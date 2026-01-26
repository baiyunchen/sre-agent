using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Agents;

/// <summary>
/// 工具执行器 - 负责执行工具调用并收集结果
/// </summary>
public class ToolExecutor
{
    private readonly ILogger _logger;

    public ToolExecutor(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// 执行工具调用列表
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="agentId">Agent ID</param>
    /// <param name="toolCalls">工具调用列表</param>
    /// <param name="tools">可用的工具列表</param>
    /// <param name="variables">共享变量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具执行结果列表</returns>
    public async Task<List<(string CallId, string ToolName, ToolResult Result)>> ExecuteAsync(
        Guid sessionId,
        string agentId,
        List<FunctionCallContent> toolCalls,
        IReadOnlyList<ITool> tools,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        var results = new List<(string, string, ToolResult)>();

        foreach (var toolCall in toolCalls)
        {
            var tool = tools.FirstOrDefault(t => t.Name == toolCall.Name);

            ToolResult result;
            if (tool == null)
            {
                // 工具不存在，返回错误结果
                result = ToolResult.Failure(
                    $"工具 '{toolCall.Name}' 不存在。可用的工具: {string.Join(", ", tools.Select(t => t.Name))}",
                    "TOOL_NOT_FOUND");
            }
            else
            {
                // 执行工具
                result = await ExecuteSingleToolAsync(
                    sessionId,
                    agentId,
                    tool,
                    toolCall,
                    variables,
                    cancellationToken);
            }

            results.Add((toolCall.CallId ?? Guid.NewGuid().ToString(), toolCall.Name, result));
        }

        return results;
    }

    /// <summary>
    /// 执行单个工具
    /// </summary>
    private async Task<ToolResult> ExecuteSingleToolAsync(
        Guid sessionId,
        string agentId,
        ITool tool,
        FunctionCallContent toolCall,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken)
    {
        var toolSw = Stopwatch.StartNew();
        ToolResult result;

        try
        {
            var parameters = toolCall.Arguments is not null
                ? JsonSerializer.SerializeToElement(toolCall.Arguments)
                : default;

            var rawArguments = parameters.ValueKind != JsonValueKind.Undefined
                ? parameters.GetRawText()
                : "{}";

            _logger.LogDebug(
                "工具 '{ToolName}' 参数: Arguments={Arguments}, Parameters.ValueKind={ValueKind}, RawArguments={RawArguments}",
                toolCall.Name,
                toolCall.Arguments is not null ? JsonSerializer.Serialize(toolCall.Arguments) : "null",
                parameters.ValueKind,
                rawArguments);

            var toolContext = new ToolExecutionContext
            {
                SessionId = sessionId,
                AgentId = agentId,
                Parameters = parameters,
                RawArguments = rawArguments,
                Variables = variables,
                ToolCallId = toolCall.CallId ?? Guid.NewGuid().ToString()
            };

            result = await tool.ExecuteAsync(toolContext, cancellationToken);
        }
        catch (Exception ex)
        {
            // 即使工具抛异常，也转为 Result 返回给 LLM
            result = ToolResult.FromException(ex);
        }

        toolSw.Stop();
        result = result with { Duration = toolSw.Elapsed };

        return result;
    }

    /// <summary>
    /// 将 ITool 转换为 AITool (用于 LLM 调用)
    /// </summary>
    public static AITool ToAITool(ITool tool)
    {
        // 使用 ToolAIFunction 适配器，它会正确传递自定义的参数 Schema
        return tool.ToAITool();
    }

    /// <summary>
    /// 将工具列表转换为 AITool 列表
    /// </summary>
    public static List<AITool> ToAITools(IReadOnlyList<ITool> tools)
    {
        return tools.Select(ToAITool).ToList();
    }
}
