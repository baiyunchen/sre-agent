using System.Diagnostics;
using System.Text.Json;
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
/// 工具循环 Agent - Framework 提供的基础实现
/// 实现标准的 ReAct 模式：Think -> Act -> Observe 循环
/// </summary>
public class ToolLoopAgent : IAgent
{
    private readonly ModelProvider _modelProvider;
    private readonly AgentOptions _options;
    private readonly ILogger<ToolLoopAgent> _logger;
    
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
        
        // 根据配置的能力级别获取对应的 ChatClient
        var chatClient = _modelProvider.GetChatClient(_options.ModelCapability);
        
        _logger.LogDebug(
            "[{ExecutionId}] 使用模型能力级别: {ModelCapability}，最大迭代次数: {MaxIterations}，工具数量: {ToolCount}",
            executionId, _options.ModelCapability, _options.MaxIterations, _options.Tools.Count);
        
        try
        {
            // 初始化消息
            InitializeMessages(messages, context);
            _logger.LogDebug("[{ExecutionId}] 消息初始化完成，初始消息数: {MessageCount}", executionId, messages.Count);
            
            // 主循环
            for (var iteration = 0; iteration < _options.MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogInformation(
                    "[{ExecutionId}] 开始第 {Iteration}/{MaxIterations} 次迭代，当前消息数: {MessageCount}，累计 Token: 输入={PromptTokens}, 输出={CompletionTokens}",
                    executionId, iteration + 1, _options.MaxIterations, messages.Count, 
                    totalTokenUsage.PromptTokens, totalTokenUsage.CompletionTokens);
                
                // 调用 LLM
                _logger.LogDebug(
                    "[{ExecutionId}] 准备调用 LLM，消息数: {MessageCount}，工具数: {ToolCount}",
                    executionId, messages.Count, _options.Tools.Count);
                
                var llmSw = Stopwatch.StartNew();
                var (response, tokenUsage) = await CallLlmAsync(chatClient, messages, cancellationToken);
                llmSw.Stop();
                totalTokenUsage += tokenUsage;
                
                _logger.LogDebug(
                    "[{ExecutionId}] LLM 调用完成，耗时: {ElapsedMs}ms，本次 Token: 输入={PromptTokens}, 输出={CompletionTokens}",
                    executionId, llmSw.ElapsedMilliseconds, tokenUsage.PromptTokens, tokenUsage.CompletionTokens);
                
                // 添加 Assistant 响应到消息历史
                messages.Add(response);
                
                // 检查是否有工具调用
                var toolCalls = response.Contents.OfType<FunctionCallContent>().ToList();
                
                if (toolCalls.Count > 0)
                {
                    var toolNames = string.Join(", ", toolCalls.Select(tc => tc.Name));
                    _logger.LogInformation(
                        "[{ExecutionId}] 迭代 {Iteration}: 检测到 {ToolCallCount} 个工具调用: [{ToolNames}]",
                        executionId, iteration + 1, toolCalls.Count, toolNames);
                    
                    // 执行工具调用
                    var toolSw = Stopwatch.StartNew();
                    var toolResults = await ExecuteToolCallsAsync(
                        context.SessionId,
                        toolCalls,
                        context.Variables,
                        cancellationToken);
                    toolSw.Stop();
                    
                    // 记录每个工具的执行结果
                    foreach (var (callId, toolName, result) in toolResults)
                    {
                        if (result.IsSuccess)
                        {
                            _logger.LogDebug(
                                "[{ExecutionId}] 工具 '{ToolName}' 执行成功，耗时: {Duration}ms，结果长度: {ResultLength}",
                                executionId, toolName, result.Duration.TotalMilliseconds, result.Content?.Length ?? 0);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[{ExecutionId}] 工具 '{ToolName}' 执行失败，错误码: {ErrorCode}，错误信息: {ErrorMessage}",
                                executionId, toolName, result.ErrorCode, result.Content);
                        }
                    }
                    
                    _logger.LogDebug(
                        "[{ExecutionId}] 所有工具执行完成，总耗时: {ElapsedMs}ms",
                        executionId, toolSw.ElapsedMilliseconds);
                    
                    // 添加工具结果消息
                    var toolResultMessage = CreateToolResultMessage(toolResults);
                    messages.Add(toolResultMessage);
                }
                else
                {
                    // 没有工具调用，Agent 完成
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
                }
            }
            
            // 达到最大迭代次数
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
        catch (OperationCanceledException)
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
        catch (Exception ex)
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
    }
    
    private void InitializeMessages(List<ChatMessage> messages, AgentExecutionContext context)
    {
        // 添加 System Prompt
        if (!string.IsNullOrEmpty(_options.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, _options.SystemPrompt));
        }
        
        // 添加初始历史消息
        if (context.InitialMessages is { Count: > 0 })
        {
            messages.AddRange(context.InitialMessages);
        }
        
        // 添加用户输入
        messages.Add(new ChatMessage(ChatRole.User, context.Input));
    }
    
    private async Task<(ChatMessage Response, TokenUsage TokenUsage)> CallLlmAsync(
        IChatClient chatClient,
        List<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var chatOptions = new ChatOptions
        {
            Temperature = (float)_options.Temperature,
            MaxOutputTokens = _options.MaxTokens,
            Tools = _options.Tools.Select(CreateAiToolFromTool).ToList()
        };
        
        var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        
        var tokenUsage = new TokenUsage(
            (int)(response.Usage?.InputTokenCount ?? 0),
            (int)(response.Usage?.OutputTokenCount ?? 0));
        
        // 返回最后一条消息（通常是 Assistant 的响应）
        var lastMessage = response.Messages.LastOrDefault() 
                          ?? new ChatMessage(ChatRole.Assistant, string.Empty);
        
        return (lastMessage, tokenUsage);
    }
    
    /// <summary>
    /// 将 ITool 转换为 AITool
    /// </summary>
    private static AITool CreateAiToolFromTool(ITool tool)
    {
        // 使用 AIFunctionFactory.Create 创建 AIFunction
        // 传入一个占位委托，实际执行在 ExecuteToolCallsAsync 中处理
        return AIFunctionFactory.Create(
            (JsonElement _) => "placeholder",
            tool.Name,
            tool.Description);
    }
    
    private async Task<List<(string CallId, string ToolName, ToolResult Result)>> ExecuteToolCallsAsync(
        Guid sessionId,
        List<FunctionCallContent> toolCalls,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken)
    {
        var results = new List<(string, string, ToolResult)>();
        
        foreach (var toolCall in toolCalls)
        {
            var tool = _options.Tools.FirstOrDefault(t => t.Name == toolCall.Name);
            
            ToolResult result;
            if (tool == null)
            {
                // 工具不存在，返回错误结果
                result = ToolResult.Failure(
                    $"工具 '{toolCall.Name}' 不存在。可用的工具: {string.Join(", ", _options.Tools.Select(t => t.Name))}",
                    "TOOL_NOT_FOUND");
            }
            else
            {
                // 执行工具
                var toolSw = Stopwatch.StartNew();
                try
                {
                    var parameters = toolCall.Arguments is not null
                        ? JsonSerializer.SerializeToElement(toolCall.Arguments)
                        : default;
                    
                    var toolContext = new ToolExecutionContext
                    {
                        SessionId = sessionId,
                        AgentId = Id,
                        Parameters = parameters,
                        RawArguments = parameters.GetRawText(),
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
            }
            
            results.Add((toolCall.CallId ?? Guid.NewGuid().ToString(), toolCall.Name, result));
        }
        
        return results;
    }
    
    private static ChatMessage CreateToolResultMessage(
        List<(string CallId, string ToolName, ToolResult Result)> toolResults)
    {
        var contents = toolResults
            .Select(tr => new FunctionResultContent(
                tr.CallId,
                tr.Result.Content))
            .Cast<AIContent>()
            .ToList();
        
        return new ChatMessage(ChatRole.Tool, contents);
    }
}
