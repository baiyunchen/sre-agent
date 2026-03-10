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
    public async Task<List<(string CallId, string ToolName, ToolResult Result)>> ExecuteAsync(
        Guid sessionId,
        string agentId,
        List<FunctionCallContent> toolCalls,
        IReadOnlyList<ITool> tools,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken = default,
        IContextManager? parentContext = null,
        Guid? agentRunId = null)
    {
        var results = new List<(string, string, ToolResult)>();
        var tracker = variables.TryGetValue(IExecutionTracker.VariableKey, out var t)
            ? t as IExecutionTracker
            : null;

        foreach (var toolCall in toolCalls)
        {
            var tool = tools.FirstOrDefault(t => t.Name == toolCall.Name);

            ToolResult result;
            if (tool == null)
            {
                result = ToolResult.Failure(
                    $"工具 '{toolCall.Name}' 不存在。可用的工具: {string.Join(", ", tools.Select(t => t.Name))}",
                    "TOOL_NOT_FOUND");
            }
            else
            {
                Guid? invocationId = null;
                var rawArgs = toolCall.Arguments is not null
                    ? JsonSerializer.Serialize(toolCall.Arguments)
                    : null;

                if (tracker != null && agentRunId.HasValue)
                {
                    try { invocationId = await tracker.OnToolStartAsync(agentRunId.Value, toolCall.Name, rawArgs, cancellationToken); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to track tool start for {ToolName}", toolCall.Name); }
                }

                result = await ExecuteSingleToolAsync(
                    sessionId, agentId, tool, toolCall, variables, parentContext, cancellationToken);

                if (tracker != null && invocationId.HasValue)
                {
                    try
                    {
                        await tracker.OnToolCompleteAsync(
                            invocationId.Value, result.IsSuccess, result.Content,
                            result.IsSuccess ? null : result.ErrorCode,
                            (long)result.Duration.TotalMilliseconds, cancellationToken);
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to track tool completion for {ToolName}", toolCall.Name); }
                }
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
        IContextManager? parentContext,
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
                "工具 '{ToolName}' 参数: {RawArguments}",
                toolCall.Name, rawArguments);

            var toolContext = new ToolExecutionContext
            {
                SessionId = sessionId,
                AgentId = agentId,
                Parameters = parameters,
                RawArguments = rawArguments,
                Variables = variables,
                ToolCallId = toolCall.CallId ?? Guid.NewGuid().ToString(),
                ParentContext = parentContext
            };

            result = await tool.ExecuteAsync(toolContext, cancellationToken);
        }
        catch (Exception ex)
        {
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
