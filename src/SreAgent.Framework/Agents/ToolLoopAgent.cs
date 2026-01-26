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
    /// <param name="id">Agent 唯一标识</param>
    /// <param name="name">Agent 名称</param>
    /// <param name="description">Agent 描述</param>
    /// <param name="modelProvider">Model Provider（全局共享）</param>
    /// <param name="options">Agent 配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
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

        // 使用传入的剪枝器，或创建默认的 RemoveOldestContextTrimmer
        _contextTrimmer = _options.ContextTrimmer ?? new RemoveOldestContextTrimmer();

        // 初始化组件
        var tokenEstimator = new SimpleTokenEstimator();
        _toolExecutor = new ToolExecutor(_logger);
        _llmCaller = new LlmCaller(_logger);
        _tokenManager = new TokenManager(tokenEstimator, _logger);
    }

    public async Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString("N")[..8];
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{ExecutionId}] Agent '{AgentName}' ({AgentId}) 开始执行，SessionId: {SessionId}，输入长度: {InputLength}",
            executionId, Name, Id, context.SessionId, context.Input?.Length ?? 0);

        var messages = new List<ChatMessage>();
        var totalTokenUsage = new TokenUsage();

        // 创建上下文管理器
        var tokenEstimator = new SimpleTokenEstimator();
        var contextManager = new DefaultContextManager(tokenEstimator);

        // 根据配置的能力级别获取对应的 ChatClient
        var chatClient = _modelProvider.GetChatClient(_options.ModelCapability);

        // 获取模型 Token 限制
        var tokenLimits = _modelProvider.Options.GetTokenLimits(_options.ModelCapability);

        _logger.LogDebug(
            "[{ExecutionId}] 使用模型能力级别: {ModelCapability}，最大迭代次数: {MaxIterations}，工具数量: {ToolCount}，有效输入 Token 限制: {EffectiveInputTokens}",
            executionId, _options.ModelCapability, _options.MaxIterations, _options.Tools.Count, tokenLimits.EffectiveInputTokens);

        try
        {
            // 初始化消息
            InitializeMessages(messages, contextManager, context);
            _logger.LogDebug("[{ExecutionId}] 消息初始化完成，初始消息数: {MessageCount}", executionId, messages.Count);

            // 主循环
            for (var iteration = 0; iteration < _options.MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "[{ExecutionId}] 开始第 {Iteration}/{MaxIterations} 次迭代，当前消息数: {MessageCount}，估算 Token: {EstimatedTokens}，累计 Token: 输入={PromptTokens}, 输出={CompletionTokens}",
                    executionId, iteration + 1, _options.MaxIterations, messages.Count,
                    contextManager.EstimatedTokenCount,
                    totalTokenUsage.PromptTokens, totalTokenUsage.CompletionTokens);

                // Token 检查和剪枝
                var effectiveLimit = tokenLimits.EffectiveInputTokens;
                var currentTokens = _tokenManager.CalculateTotalTokens(contextManager, _options.Tools);

                if (_tokenManager.NeedsTrimming(currentTokens, effectiveLimit))
                {
                    _logger.LogInformation(
                        "[{ExecutionId}] Token 超限，当前: {Current}，限制: {Limit}，触发 {Trimmer} 剪枝",
                        executionId, currentTokens, effectiveLimit, _contextTrimmer.Name);

                    var trimResult = _tokenManager.TryTrim(
                        contextManager,
                        _contextTrimmer,
                        effectiveLimit,
                        _options.TrimTargetRatio,
                        _options.Tools);

                    if (trimResult.IsSuccess)
                    {
                        // 重建 ChatMessage 列表
                        messages = MessageConverter.RebuildFromContext(contextManager);
                    }
                }

                // 调用 LLM
                _logger.LogDebug(
                    "[{ExecutionId}] 准备调用 LLM，消息数: {MessageCount}，工具数: {ToolCount}",
                    executionId, messages.Count, _options.Tools.Count);

                var llmSw = Stopwatch.StartNew();
                var (response, tokenUsage) = await _llmCaller.CallAsync(
                    chatClient,
                    messages,
                    _options.Tools,
                    _options.Temperature,
                    _options.MaxTokens,
                    cancellationToken);
                llmSw.Stop();
                totalTokenUsage += tokenUsage;

                _logger.LogDebug(
                    "[{ExecutionId}] LLM 调用完成，耗时: {ElapsedMs}ms，本次 Token: 输入={PromptTokens}, 输出={CompletionTokens}",
                    executionId, llmSw.ElapsedMilliseconds, tokenUsage.PromptTokens, tokenUsage.CompletionTokens);

                // 添加 Assistant 响应到消息历史
                messages.Add(response);
                AddAssistantMessageToContext(contextManager, response);

                // 检查是否有工具调用
                var toolCalls = response.Contents.OfType<FunctionCallContent>().ToList();

                if (toolCalls.Count > 0)
                {
                    var result = await HandleToolCalls(
                        executionId,
                        iteration,
                        context,
                        toolCalls,
                        messages,
                        contextManager,
                        cancellationToken);

                    if (result != null)
                    {
                        return result;
                    }
                }
                else
                {
                    // 没有工具调用，检查是否完成
                    var completionResult = TryGetCompletionResult(
                        executionId,
                        iteration,
                        response,
                        messages,
                        totalTokenUsage,
                        sw);

                    if (completionResult != null)
                    {
                        return completionResult;
                    }
                }
            }

            // 达到最大迭代次数
            return HandleMaxIterationsReached(executionId, messages, totalTokenUsage, sw);
        }
        catch (OperationCanceledException)
        {
            return HandleCancellation(executionId, messages, totalTokenUsage, sw);
        }
        catch (Exception ex)
        {
            return HandleException(executionId, ex, messages, totalTokenUsage, sw);
        }
    }

    #region 消息初始化

    private void InitializeMessages(List<ChatMessage> messages, IContextManager contextManager, AgentExecutionContext context)
    {
        // 添加 System Prompt
        if (!string.IsNullOrEmpty(_options.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, _options.SystemPrompt));
            contextManager.SetSystemMessage(_options.SystemPrompt);
        }

        // 添加初始历史消息
        if (context.InitialMessages is { Count: > 0 })
        {
            messages.AddRange(context.InitialMessages);
            foreach (var msg in context.InitialMessages)
            {
                contextManager.AddMessage(MessageConverter.FromChatMessage(msg));
            }
        }

        // 添加用户输入
        messages.Add(new ChatMessage(ChatRole.User, context.Input));
        contextManager.AddMessage(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Text = context.Input }]
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
        string executionId,
        int iteration,
        AgentExecutionContext context,
        List<FunctionCallContent> toolCalls,
        List<ChatMessage> messages,
        IContextManager contextManager,
        CancellationToken cancellationToken)
    {
        var toolNames = string.Join(", ", toolCalls.Select(tc => tc.Name));
        _logger.LogInformation(
            "[{ExecutionId}] 迭代 {Iteration}: 检测到 {ToolCallCount} 个工具调用: [{ToolNames}]",
            executionId, iteration + 1, toolCalls.Count, toolNames);

        // 执行工具调用
        var toolSw = Stopwatch.StartNew();
        var toolResults = await _toolExecutor.ExecuteAsync(
            context.SessionId,
            Id,
            toolCalls,
            _options.Tools,
            context.Variables,
            cancellationToken);
        toolSw.Stop();

        // 记录每个工具的执行结果
        LogToolResults(executionId, toolResults);

        _logger.LogDebug(
            "[{ExecutionId}] 所有工具执行完成，总耗时: {ElapsedMs}ms",
            executionId, toolSw.ElapsedMilliseconds);

        // 添加工具结果消息
        var toolResultMessage = MessageConverter.CreateToolResultMessage(toolResults);
        messages.Add(toolResultMessage);
        contextManager.AddMessage(MessageConverter.CreateToolResultInternalMessage(toolResults));

        return null; // 继续循环
    }

    private void LogToolResults(string executionId, List<(string CallId, string ToolName, ToolResult Result)> toolResults)
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

    private AgentResult? TryGetCompletionResult(
        string executionId,
        int iteration,
        ChatMessage response,
        List<ChatMessage> messages,
        TokenUsage totalTokenUsage,
        Stopwatch sw)
    {
        var textContent = response.Contents
            .OfType<TextContent>()
            .Select(t => t.Text)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(textContent))
        {
            sw.Stop();
            _logger.LogInformation(
                "[{ExecutionId}] Agent '{AgentName}' 执行成功完成，迭代次数: {Iterations}，总耗时: {ElapsedMs}ms，累计 Token: 输入={PromptTokens}, 输出={CompletionTokens}，响应长度: {ResponseLength}",
                executionId, Name, iteration + 1, sw.ElapsedMilliseconds,
                totalTokenUsage.PromptTokens, totalTokenUsage.CompletionTokens, textContent.Length);

            return AgentResult.Success(
                textContent,
                messages,
                totalTokenUsage,
                iteration + 1);
        }

        _logger.LogDebug(
            "[{ExecutionId}] 迭代 {Iteration}: 无工具调用且无文本内容，继续下一次迭代",
            executionId, iteration + 1);

        return null;
    }

    private AgentResult HandleMaxIterationsReached(
        string executionId,
        List<ChatMessage> messages,
        TokenUsage totalTokenUsage,
        Stopwatch sw)
    {
        sw.Stop();
        _logger.LogWarning(
            "[{ExecutionId}] Agent '{AgentName}' 达到最大迭代次数 {MaxIterations}，总耗时: {ElapsedMs}ms，累计 Token: 输入={PromptTokens}, 输出={CompletionTokens}",
            executionId, Name, _options.MaxIterations, sw.ElapsedMilliseconds,
            totalTokenUsage.PromptTokens, totalTokenUsage.CompletionTokens);

        return AgentResult.Failure(
            new AgentError("MAX_ITERATIONS", "Reached maximum iterations without completion"),
            messages,
            totalTokenUsage);
    }

    private AgentResult HandleCancellation(
        string executionId,
        List<ChatMessage> messages,
        TokenUsage totalTokenUsage,
        Stopwatch sw)
    {
        sw.Stop();
        _logger.LogWarning(
            "[{ExecutionId}] Agent '{AgentName}' 执行被取消，总耗时: {ElapsedMs}ms",
            executionId, Name, sw.ElapsedMilliseconds);

        return AgentResult.Failure(
            new AgentError("CANCELLED", "Operation was cancelled"),
            messages,
            totalTokenUsage,
            isRetryable: false);
    }

    private AgentResult HandleException(
        string executionId,
        Exception ex,
        List<ChatMessage> messages,
        TokenUsage totalTokenUsage,
        Stopwatch sw)
    {
        sw.Stop();
        _logger.LogError(ex,
            "[{ExecutionId}] Agent '{AgentName}' 执行异常，总耗时: {ElapsedMs}ms，异常类型: {ExceptionType}，异常信息: {ExceptionMessage}",
            executionId, Name, sw.ElapsedMilliseconds, ex.GetType().Name, ex.Message);

        return AgentResult.Failure(
            new AgentError("EXCEPTION", ex.Message, ex),
            messages,
            totalTokenUsage);
    }

    #endregion
}
