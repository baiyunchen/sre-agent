using System.Diagnostics;
using System.Text.Json;
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
                var toolDefinitionTokens = EstimateToolDefinitionTokens(tokenEstimator);
                var currentTokens = contextManager.EstimatedTokenCount + toolDefinitionTokens;
                
                if (currentTokens > effectiveLimit)
                {
                    _logger.LogInformation(
                        "[{ExecutionId}] Token 超限，当前: {Current}，限制: {Limit}，触发 {Trimmer} 剪枝",
                        executionId, currentTokens, effectiveLimit, _contextTrimmer.Name);
                    
                    var targetTokens = (int)(effectiveLimit * _options.TrimTargetRatio) - toolDefinitionTokens;
                    var trimResult = _contextTrimmer.Trim(contextManager, targetTokens);
                    
                    if (trimResult.IsSuccess)
                    {
                        _logger.LogInformation(
                            "[{ExecutionId}] 剪枝完成，Token: {Before} -> {After}，移除消息: {Removed}",
                            executionId, trimResult.TokensBefore, trimResult.TokensAfter, trimResult.MessagesRemoved);
                        
                        // 重建 ChatMessage 列表
                        messages = RebuildChatMessages(contextManager);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[{ExecutionId}] 剪枝失败: {Error}",
                            executionId, trimResult.ErrorMessage);
                    }
                }
                
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
                AddAssistantMessageToContext(contextManager, response);
                
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
                    
                    _logger.LogDebug(
                        "[{ExecutionId}] 所有工具执行完成，总耗时: {ElapsedMs}ms",
                        executionId, toolSw.ElapsedMilliseconds);
                    
                    // 添加工具结果消息
                    var toolResultMessage = CreateToolResultMessage(toolResults);
                    messages.Add(toolResultMessage);
                    AddToolResultsToContext(contextManager, toolResults);
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
            // 将初始消息添加到上下文管理器
            foreach (var msg in context.InitialMessages)
            {
                contextManager.AddMessage(ConvertToMessage(msg));
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
    
    private static Message ConvertToMessage(ChatMessage chatMessage)
    {
        var role = chatMessage.Role.Value switch
        {
            "system" => MessageRole.System,
            "user" => MessageRole.User,
            "assistant" => MessageRole.Assistant,
            "tool" => MessageRole.Tool,
            _ => MessageRole.User
        };
        
        var parts = new List<MessagePart>();
        
        foreach (var content in chatMessage.Contents)
        {
            switch (content)
            {
                case TextContent textContent:
                    parts.Add(new TextPart { Text = textContent.Text ?? string.Empty });
                    break;
                case FunctionCallContent functionCall:
                    parts.Add(new ToolCallPart
                    {
                        ToolCallId = functionCall.CallId ?? string.Empty,
                        Name = functionCall.Name,
                        Arguments = functionCall.Arguments is not null 
                            ? JsonSerializer.Serialize(functionCall.Arguments) 
                            : "{}"
                    });
                    break;
                case FunctionResultContent functionResult:
                    parts.Add(new ToolResultPart
                    {
                        ToolCallId = functionResult.CallId ?? string.Empty,
                        ToolName = string.Empty, // FunctionResultContent doesn't have Name property
                        IsSuccess = true,
                        Content = functionResult.Result?.ToString() ?? string.Empty
                    });
                    break;
            }
        }
        
        return new Message
        {
            Role = role,
            Parts = parts
        };
    }
    
    private void AddAssistantMessageToContext(IContextManager contextManager, ChatMessage response)
    {
        var message = ConvertToMessage(response);
        message.Metadata.AgentId = Id;
        contextManager.AddMessage(message);
    }
    
    private void AddToolResultsToContext(
        IContextManager contextManager, 
        List<(string CallId, string ToolName, ToolResult Result)> toolResults)
    {
        var parts = toolResults.Select(tr => (MessagePart)new ToolResultPart
        {
            ToolCallId = tr.CallId,
            ToolName = tr.ToolName,
            IsSuccess = tr.Result.IsSuccess,
            Content = tr.Result.Content
        }).ToList();
        
        contextManager.AddMessage(new Message
        {
            Role = MessageRole.Tool,
            Parts = parts
        });
    }
    
    private List<ChatMessage> RebuildChatMessages(IContextManager contextManager)
    {
        var result = new List<ChatMessage>();
        
        foreach (var message in contextManager.GetMessages())
        {
            var chatRole = message.Role switch
            {
                MessageRole.System => ChatRole.System,
                MessageRole.User => ChatRole.User,
                MessageRole.Assistant => ChatRole.Assistant,
                MessageRole.Tool => ChatRole.Tool,
                _ => ChatRole.User
            };
            
            var contents = new List<AIContent>();
            
            foreach (var part in message.Parts)
            {
                switch (part)
                {
                    case TextPart textPart:
                        contents.Add(new TextContent(textPart.Text));
                        break;
                    case ToolCallPart toolCallPart:
                        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCallPart.Arguments);
                        contents.Add(new FunctionCallContent(toolCallPart.ToolCallId, toolCallPart.Name, args));
                        break;
                    case ToolResultPart toolResultPart:
                        contents.Add(new FunctionResultContent(toolResultPart.ToolCallId, toolResultPart.Content));
                        break;
                }
            }
            
            result.Add(new ChatMessage(chatRole, contents));
        }
        
        return result;
    }
    
    private int EstimateToolDefinitionTokens(ITokenEstimator tokenEstimator)
    {
        // 估算工具定义的 Token 数
        var totalTokens = 0;
        foreach (var tool in _options.Tools)
        {
            var detail = tool.GetDetail();
            // 工具名称 + 描述 + 参数 schema 的估算
            totalTokens += tokenEstimator.EstimateTokens(detail.Name);
            totalTokens += tokenEstimator.EstimateTokens(detail.Description);
            totalTokens += tokenEstimator.EstimateTokens(detail.ParameterSchema);
        }
        return totalTokens;
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
        
        // 打印原始 API 响应，方便调试
        _logger.LogInformation("[LLM Response] FinishReason={FinishReason}, MessageCount={MessageCount}",
            response.FinishReason, response.Messages.Count);
        
        foreach (var msg in response.Messages)
        {
            _logger.LogInformation("[LLM Response] Message Role={Role}, ContentCount={ContentCount}",
                msg.Role, msg.Contents.Count);
            
            foreach (var content in msg.Contents)
            {
                switch (content)
                {
                    case TextContent textContent:
                        _logger.LogInformation("[LLM Response] TextContent: {Text}", 
                            textContent.Text?.Length > 200 ? textContent.Text[..200] + "..." : textContent.Text);
                        break;
                    case FunctionCallContent functionCall:
                        // 打印工具调用的原始参数
                        var argsJson = functionCall.Arguments != null 
                            ? JsonSerializer.Serialize(functionCall.Arguments) 
                            : "null";
                        _logger.LogInformation(
                            "[LLM Response] FunctionCall: Name={Name}, CallId={CallId}, Arguments={Arguments}",
                            functionCall.Name, functionCall.CallId, argsJson);
                        break;
                    default:
                        _logger.LogInformation("[LLM Response] OtherContent: Type={Type}", 
                            content.GetType().Name);
                        break;
                }
            }
        }
        
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
    private AITool CreateAiToolFromTool(ITool tool)
    {
        var detail = tool.GetDetail();
        
        // 打印工具定义，方便调试
        _logger.LogInformation(
            "[CreateAiTool] Name={Name}, ParameterSchema={Schema}",
            detail.Name, detail.ParameterSchema);
        
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
                    
                    var rawArguments = parameters.ValueKind != JsonValueKind.Undefined 
                        ? parameters.GetRawText() 
                        : "{}";
                    
                    _logger.LogDebug(
                        "[{ExecutionId}] 工具 '{ToolName}' 参数: Arguments={Arguments}, Parameters.ValueKind={ValueKind}, RawArguments={RawArguments}",
                        Guid.NewGuid().ToString("N")[..8], toolCall.Name, 
                        toolCall.Arguments is not null ? JsonSerializer.Serialize(toolCall.Arguments) : "null",
                        parameters.ValueKind,
                        rawArguments);
                    
                    var toolContext = new ToolExecutionContext
                    {
                        SessionId = sessionId,
                        AgentId = Id,
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
