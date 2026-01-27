using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Options;
using SreAgent.Framework.Providers;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Agents;

/// <summary>
/// 工具循环 Agent - 实现 ReAct 模式的执行引擎
/// 
/// Agent 是无状态的，可被多个请求复用。
/// 所有对话状态和剪枝逻辑都在 IContextManager 中管理。
/// </summary>
public class ToolLoopAgent : IAgent
{
    private readonly ModelProvider _modelProvider;
    private readonly ILogger<ToolLoopAgent> _logger;
    private readonly ToolExecutor _toolExecutor;
    private readonly LlmCaller _llmCaller;
    private readonly ITokenEstimator _tokenEstimator;

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public AgentOptions Options { get; }

    public ToolLoopAgent(
        string id,
        string name,
        string description,
        ModelProvider modelProvider,
        AgentOptions? options = null,
        ILogger<ToolLoopAgent>? logger = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Options = options ?? new AgentOptions();
        _modelProvider = modelProvider;
        _logger = logger ?? NullLogger<ToolLoopAgent>.Instance;

        _tokenEstimator = new SimpleTokenEstimator();
        _toolExecutor = new ToolExecutor(_logger);
        _llmCaller = new LlmCaller(_logger);
    }

    /// <summary>
    /// 执行 Agent
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(
        IContextManager context,
        IReadOnlyDictionary<string, object>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();
        var vars = variables ?? new Dictionary<string, object>();

        // 配置 Token 限制（用于 ContextManager 自动剪枝）
        ConfigureContextTokenLimit(context);

        _logger.LogInformation(
            "[{ExecutionId}] Agent '{Name}' 开始执行，SessionId: {SessionId}，消息数: {Count}",
            executionId, Name, context.SessionId, context.GetMessages().Count);

        try
        {
            return await RunMainLoop(context, vars, executionId, stopwatch, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return CreateFailureResult(context, stopwatch,
                new AgentError("CANCELLED", "Operation was cancelled"), isRetryable: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ExecutionId}] 执行异常: {Message}", executionId, ex.Message);
            return CreateFailureResult(context, stopwatch, new AgentError("EXCEPTION", ex.Message, ex));
        }
    }

    /// <summary>
    /// 配置 ContextManager 的 Token 限制
    /// </summary>
    private void ConfigureContextTokenLimit(IContextManager context)
    {
        var tokenLimit = _modelProvider.Options.GetTokenLimits(Options.ModelCapability).EffectiveInputTokens;
        var toolTokens = EstimateToolDefinitionTokens(Options.Tools);
        context.ConfigureTokenLimit(tokenLimit, toolTokens);
    }

    /// <summary>
    /// 估算工具定义的 Token 数
    /// </summary>
    private int EstimateToolDefinitionTokens(IReadOnlyList<ITool> tools)
    {
        var totalTokens = 0;
        foreach (var tool in tools)
        {
            var detail = tool.GetDetail();
            totalTokens += _tokenEstimator.EstimateTokens(detail.Name);
            totalTokens += _tokenEstimator.EstimateTokens(detail.Description);
            totalTokens += _tokenEstimator.EstimateTokens(detail.ParameterSchema);
        }
        return totalTokens;
    }

    /// <summary>
    /// 主执行循环
    /// </summary>
    private async Task<AgentResult> RunMainLoop(
        IContextManager context,
        IReadOnlyDictionary<string, object> variables,
        string executionId,
        Stopwatch stopwatch,
        CancellationToken ct)
    {
        var chatClient = _modelProvider.GetChatClient(Options.ModelCapability);

        for (var iteration = 0; iteration < Options.MaxIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();
            LogIterationStart(executionId, iteration, context);

            // 获取消息（ContextManager 内部自动剪枝）
            var messages = context.GetChatMessages();

            // 调用 LLM
            var (response, tokenUsage) = await _llmCaller.CallAsync(
                chatClient, messages, Options.Tools, Options.Temperature, Options.MaxTokens, ct);

            // 添加响应并记录 Token 使用
            context.AddAssistantMessage(response, tokenUsage, Id);

            // 检查是否需要调用工具
            var toolCalls = response.Contents.OfType<FunctionCallContent>().ToList();
            if (toolCalls.Count > 0)
            {
                await HandleToolCallsAsync(context, variables, toolCalls, executionId, iteration, ct);
                continue;
            }

            // 检查是否有最终回复
            var output = ExtractTextContent(response);
            if (!string.IsNullOrEmpty(output))
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "[{ExecutionId}] 执行完成，迭代: {Iterations}，耗时: {Ms}ms",
                    executionId, iteration + 1, stopwatch.ElapsedMilliseconds);

                return AgentResult.Success(output, context, context.TotalTokenUsage, iteration + 1);
            }
        }

        _logger.LogWarning("[{ExecutionId}] 达到最大迭代次数 {Max}", executionId, Options.MaxIterations);
        return CreateFailureResult(context, stopwatch,
            new AgentError("MAX_ITERATIONS", "Reached maximum iterations"), context.TotalTokenUsage);
    }

    private async Task HandleToolCallsAsync(
        IContextManager context,
        IReadOnlyDictionary<string, object> variables,
        List<FunctionCallContent> toolCalls,
        string executionId,
        int iteration,
        CancellationToken ct)
    {
        var names = string.Join(", ", toolCalls.Select(t => t.Name));
        _logger.LogInformation("[{ExecutionId}] 迭代 {Iteration}: 调用工具 [{Names}]",
            executionId, iteration + 1, names);

        var toolResults = await _toolExecutor.ExecuteAsync(
            context.SessionId, Id, toolCalls, Options.Tools, variables, ct, context);

        context.AddToolResultMessage(toolResults);
    }

    private static string? ExtractTextContent(ChatMessage response)
        => response.Contents.OfType<TextContent>().Select(t => t.Text).FirstOrDefault();

    private static AgentResult CreateFailureResult(
        IContextManager context, Stopwatch stopwatch, AgentError error,
        TokenUsage? tokenUsage = null, bool isRetryable = true)
    {
        stopwatch.Stop();
        return AgentResult.Failure(error, context, tokenUsage ?? context.TotalTokenUsage, isRetryable);
    }

    private void LogIterationStart(string executionId, int iteration, IContextManager context)
    {
        _logger.LogDebug("[{ExecutionId}] 迭代 {Current}/{Max}，消息: {Count}，Token: {Tokens}",
            executionId, iteration + 1, Options.MaxIterations,
            context.GetMessages().Count, context.EstimatedTokenCount);
    }
}
