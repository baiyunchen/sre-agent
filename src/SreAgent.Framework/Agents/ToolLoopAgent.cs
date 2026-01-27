using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Contexts.Trimmers;
using SreAgent.Framework.Options;
using SreAgent.Framework.Providers;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Agents;

/// <summary>
/// 工具循环 Agent - Framework 提供的基础实现
/// 实现标准的 ReAct 模式：Think -> Act -> Observe 循环
/// </summary>
public class ToolLoopAgent : IAgent
{
    private readonly ModelProvider _modelProvider;
    private readonly AgentOptions _options;
    private readonly ILogger<ToolLoopAgent> _logger;
    private readonly IContextTrimmer _contextTrimmer;
    private readonly ToolExecutor _toolExecutor;
    private readonly LlmCaller _llmCaller;
    private readonly TokenManager _tokenManager;

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>
    /// 创建 ToolLoopAgent
    /// </summary>
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
        _modelProvider = modelProvider;
        _options = options ?? new AgentOptions();
        _logger = logger ?? NullLogger<ToolLoopAgent>.Instance;

        _contextTrimmer = _options.ContextTrimmer ?? new RemoveOldestContextTrimmer();

        var tokenEstimator = new SimpleTokenEstimator();
        _toolExecutor = new ToolExecutor(_logger);
        _llmCaller = new LlmCaller(_logger);
        _tokenManager = new TokenManager(tokenEstimator, _logger);
    }

    public async Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var state = CreateExecutionState(context);
        LogExecutionStart(state);

        try
        {
            InitializeMessages(state);
            return await RunMainLoop(state, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return HandleCancellation(state);
        }
        catch (Exception ex)
        {
            return HandleException(state, ex);
        }
    }

    #region 执行状态管理

    private AgentExecutionState CreateExecutionState(AgentExecutionContext context)
    {
        return AgentExecutionState.Create(
            context,
            _modelProvider.GetChatClient(_options.ModelCapability),
            _modelProvider.Options.GetTokenLimits(_options.ModelCapability));
    }

    #endregion

    #region 主循环

    private async Task<AgentResult> RunMainLoop(AgentExecutionState state, CancellationToken cancellationToken)
    {
        for (state.CurrentIteration = 0; state.CurrentIteration < _options.MaxIterations; state.CurrentIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LogIterationStart(state);
            TrimContextIfNeeded(state);

            var iterationResult = await ProcessSingleIteration(state, cancellationToken);
            if (iterationResult != null)
            {
                return iterationResult;
            }
        }

        return HandleMaxIterationsReached(state);
    }

    private async Task<AgentResult?> ProcessSingleIteration(AgentExecutionState state, CancellationToken cancellationToken)
    {
        var response = await CallLlmAndLogResult(state, cancellationToken);

        state.Messages.Add(response);
        AddAssistantMessageToContext(state.ContextManager, response);

        var toolCalls = response.Contents.OfType<FunctionCallContent>().ToList();

        if (toolCalls.Count > 0)
        {
            return await HandleToolCalls(state, toolCalls, cancellationToken);
        }

        return TryGetCompletionResult(state, response);
    }

    #endregion

    #region LLM 调用

    private async Task<ChatMessage> CallLlmAndLogResult(AgentExecutionState state, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "[{ExecutionId}] 准备调用 LLM，消息数: {MessageCount}，工具数: {ToolCount}",
            state.ExecutionId, state.Messages.Count, _options.Tools.Count);

        var llmSw = Stopwatch.StartNew();
        var (response, tokenUsage) = await _llmCaller.CallAsync(
            state.ChatClient,
            state.Messages,
            _options.Tools,
            _options.Temperature,
            _options.MaxTokens,
            cancellationToken);
        llmSw.Stop();

        state.TotalTokenUsage += tokenUsage;

        _logger.LogDebug(
            "[{ExecutionId}] LLM 调用完成，耗时: {ElapsedMs}ms，本次 Token: 输入={PromptTokens}, 输出={CompletionTokens}",
            state.ExecutionId, llmSw.ElapsedMilliseconds, tokenUsage.PromptTokens, tokenUsage.CompletionTokens);

        return response;
    }

    #endregion

    #region Token 剪枝

    private void TrimContextIfNeeded(AgentExecutionState state)
    {
        var effectiveLimit = state.TokenLimits.EffectiveInputTokens;
        var currentTokens = _tokenManager.CalculateTotalTokens(state.ContextManager, _options.Tools);

        if (!_tokenManager.NeedsTrimming(currentTokens, effectiveLimit))
        {
            return;
        }

        _logger.LogInformation(
            "[{ExecutionId}] Token 超限，当前: {Current}，限制: {Limit}，触发 {Trimmer} 剪枝",
            state.ExecutionId, currentTokens, effectiveLimit, _contextTrimmer.Name);

        var trimResult = _tokenManager.TryTrim(
            state.ContextManager,
            _contextTrimmer,
            effectiveLimit,
            _options.TrimTargetRatio,
            _options.Tools);

        if (trimResult.IsSuccess)
        {
            state.Messages.Clear();
            state.Messages.AddRange(MessageConverter.RebuildFromContext(state.ContextManager));
        }
    }

    #endregion

    #region 消息初始化

    private void InitializeMessages(AgentExecutionState state)
    {
        AddSystemPromptIfExists(state);
        AddInitialHistoryMessages(state);
        AddUserInput(state);

        _logger.LogDebug("[{ExecutionId}] 消息初始化完成，初始消息数: {MessageCount}",
            state.ExecutionId, state.Messages.Count);
    }

    private void AddSystemPromptIfExists(AgentExecutionState state)
    {
        if (string.IsNullOrEmpty(_options.SystemPrompt)) return;

        state.Messages.Add(new ChatMessage(ChatRole.System, _options.SystemPrompt));
        state.ContextManager.SetSystemMessage(_options.SystemPrompt);
    }

    private void AddInitialHistoryMessages(AgentExecutionState state)
    {
        if (state.Context.InitialMessages is not { Count: > 0 }) return;

        state.Messages.AddRange(state.Context.InitialMessages);
        foreach (var msg in state.Context.InitialMessages)
        {
            state.ContextManager.AddMessage(MessageConverter.FromChatMessage(msg));
        }
    }

    private void AddUserInput(AgentExecutionState state)
    {
        state.Messages.Add(new ChatMessage(ChatRole.User, state.Context.Input));
        state.ContextManager.AddMessage(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Text = state.Context.Input }]
        });
    }

    private void AddAssistantMessageToContext(IContextManager contextManager, ChatMessage response)
    {
        var message = MessageConverter.FromChatMessage(response);
        message.Metadata.AgentId = Id;
        contextManager.AddMessage(message);
    }

    #endregion

    #region 工具调用处理

    private async Task<AgentResult?> HandleToolCalls(
        AgentExecutionState state,
        List<FunctionCallContent> toolCalls,
        CancellationToken cancellationToken)
    {
        LogToolCallsDetected(state, toolCalls);

        var toolResults = await ExecuteToolCalls(state, toolCalls, cancellationToken);
        LogToolResults(state.ExecutionId, toolResults);
        AddToolResultsToMessages(state, toolResults);

        return null; // 继续循环
    }

    private void LogToolCallsDetected(AgentExecutionState state, List<FunctionCallContent> toolCalls)
    {
        var toolNames = string.Join(", ", toolCalls.Select(tc => tc.Name));
        _logger.LogInformation(
            "[{ExecutionId}] 迭代 {Iteration}: 检测到 {ToolCallCount} 个工具调用: [{ToolNames}]",
            state.ExecutionId, state.CurrentIteration + 1, toolCalls.Count, toolNames);
    }

    private async Task<List<(string CallId, string ToolName, ToolResult Result)>> ExecuteToolCalls(
        AgentExecutionState state,
        List<FunctionCallContent> toolCalls,
        CancellationToken cancellationToken)
    {
        var toolSw = Stopwatch.StartNew();
        var toolResults = await _toolExecutor.ExecuteAsync(
            state.Context.SessionId,
            Id,
            toolCalls,
            _options.Tools,
            state.Context.Variables,
            cancellationToken);
        toolSw.Stop();

        _logger.LogDebug(
            "[{ExecutionId}] 所有工具执行完成，总耗时: {ElapsedMs}ms",
            state.ExecutionId, toolSw.ElapsedMilliseconds);

        return toolResults;
    }

    private void AddToolResultsToMessages(
        AgentExecutionState state,
        List<(string CallId, string ToolName, ToolResult Result)> toolResults)
    {
        state.Messages.Add(MessageConverter.CreateToolResultMessage(toolResults));
        state.ContextManager.AddMessage(MessageConverter.CreateToolResultInternalMessage(toolResults));
    }

    private void LogToolResults(
        string executionId,
        List<(string CallId, string ToolName, ToolResult Result)> toolResults)
    {
        foreach (var (_, toolName, result) in toolResults)
        {
            if (result.IsSuccess)
            {
                _logger.LogDebug(
                    "[{ExecutionId}] 工具 '{ToolName}' 执行成功，耗时: {Duration}ms，结果长度: {ResultLength}",
                    executionId, toolName, result.Duration.TotalMilliseconds, result.Content.Length);
            }
            else
            {
                _logger.LogWarning(
                    "[{ExecutionId}] 工具 '{ToolName}' 执行失败，错误码: {ErrorCode}，错误信息: {ErrorMessage}",
                    executionId, toolName, result.ErrorCode, result.Content);
            }
        }
    }

    #endregion

    #region 结果处理

    private AgentResult? TryGetCompletionResult(AgentExecutionState state, ChatMessage response)
    {
        var textContent = response.Contents
            .OfType<TextContent>()
            .Select(t => t.Text)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(textContent))
        {
            _logger.LogDebug(
                "[{ExecutionId}] 迭代 {Iteration}: 无工具调用且无文本内容，继续下一次迭代",
                state.ExecutionId, state.CurrentIteration + 1);
            return null;
        }

        return CreateSuccessResult(state, textContent);
    }

    private AgentResult CreateSuccessResult(AgentExecutionState state, string textContent)
    {
        state.Stopwatch.Stop();
        _logger.LogInformation(
            "[{ExecutionId}] Agent '{AgentName}' 执行成功完成，迭代次数: {Iterations}，总耗时: {ElapsedMs}ms，累计 Token: 输入={PromptTokens}, 输出={CompletionTokens}，响应长度: {ResponseLength}",
            state.ExecutionId, Name, state.CurrentIteration + 1, state.Stopwatch.ElapsedMilliseconds,
            state.TotalTokenUsage.PromptTokens, state.TotalTokenUsage.CompletionTokens, textContent.Length);

        return AgentResult.Success(textContent, state.Messages, state.TotalTokenUsage, state.CurrentIteration + 1);
    }

    private AgentResult HandleMaxIterationsReached(AgentExecutionState state)
    {
        state.Stopwatch.Stop();
        _logger.LogWarning(
            "[{ExecutionId}] Agent '{AgentName}' 达到最大迭代次数 {MaxIterations}，总耗时: {ElapsedMs}ms，累计 Token: 输入={PromptTokens}, 输出={CompletionTokens}",
            state.ExecutionId, Name, _options.MaxIterations, state.Stopwatch.ElapsedMilliseconds,
            state.TotalTokenUsage.PromptTokens, state.TotalTokenUsage.CompletionTokens);

        return AgentResult.Failure(
            new AgentError("MAX_ITERATIONS", "Reached maximum iterations without completion"),
            state.Messages,
            state.TotalTokenUsage);
    }

    private AgentResult HandleCancellation(AgentExecutionState state)
    {
        state.Stopwatch.Stop();
        _logger.LogWarning(
            "[{ExecutionId}] Agent '{AgentName}' 执行被取消，总耗时: {ElapsedMs}ms",
            state.ExecutionId, Name, state.Stopwatch.ElapsedMilliseconds);

        return AgentResult.Failure(
            new AgentError("CANCELLED", "Operation was cancelled"),
            state.Messages,
            state.TotalTokenUsage,
            isRetryable: false);
    }

    private AgentResult HandleException(AgentExecutionState state, Exception ex)
    {
        state.Stopwatch.Stop();
        _logger.LogError(ex,
            "[{ExecutionId}] Agent '{AgentName}' 执行异常，总耗时: {ElapsedMs}ms，异常类型: {ExceptionType}，异常信息: {ExceptionMessage}",
            state.ExecutionId, Name, state.Stopwatch.ElapsedMilliseconds, ex.GetType().Name, ex.Message);

        return AgentResult.Failure(
            new AgentError("EXCEPTION", ex.Message, ex),
            state.Messages,
            state.TotalTokenUsage);
    }

    #endregion

    #region 日志记录

    private void LogExecutionStart(AgentExecutionState state)
    {
        _logger.LogInformation(
            "[{ExecutionId}] Agent '{AgentName}' ({AgentId}) 开始执行，SessionId: {SessionId}，输入长度: {InputLength}",
            state.ExecutionId, Name, Id, state.Context.SessionId, state.Context.Input?.Length ?? 0);

        _logger.LogDebug(
            "[{ExecutionId}] 使用模型能力级别: {ModelCapability}，最大迭代次数: {MaxIterations}，工具数量: {ToolCount}，有效输入 Token 限制: {EffectiveInputTokens}",
            state.ExecutionId, _options.ModelCapability, _options.MaxIterations, _options.Tools.Count, state.TokenLimits.EffectiveInputTokens);
    }

    private void LogIterationStart(AgentExecutionState state)
    {
        _logger.LogInformation(
            "[{ExecutionId}] 开始第 {Iteration}/{MaxIterations} 次迭代，当前消息数: {MessageCount}，估算 Token: {EstimatedTokens}，累计 Token: 输入={PromptTokens}, 输出={CompletionTokens}",
            state.ExecutionId, state.CurrentIteration + 1, _options.MaxIterations, state.Messages.Count,
            state.ContextManager.EstimatedTokenCount,
            state.TotalTokenUsage.PromptTokens, state.TotalTokenUsage.CompletionTokens);
    }

    #endregion
}
