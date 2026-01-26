using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Options;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Agents;

/// <summary>
/// 工具循环 Agent - Framework 提供的基础实现
/// 实现标准的 ReAct 模式：Think -> Act -> Observe 循环
/// </summary>
public class ToolLoopAgent : IAgent
{
    private readonly IChatClient _chatClient;
    private readonly AgentOptions _options;
    
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    
    public ToolLoopAgent(
        string id,
        string name,
        string description,
        IChatClient chatClient,
        AgentOptions? options = null)
    {
        Id = id;
        Name = name;
        Description = description;
        _chatClient = chatClient;
        _options = options ?? new AgentOptions();
    }
    
    public async Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();
        var totalTokenUsage = new TokenUsage();
        
        try
        {
            // 初始化消息
            InitializeMessages(messages, context);
            
            // 主循环
            for (var iteration = 0; iteration < _options.MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 调用 LLM
                var (response, tokenUsage) = await CallLlmAsync(messages, cancellationToken);
                totalTokenUsage += tokenUsage;
                
                // 添加 Assistant 响应到消息历史
                messages.Add(response);
                
                // 检查是否有工具调用
                var toolCalls = response.Contents.OfType<FunctionCallContent>().ToList();
                
                if (toolCalls.Count > 0)
                {
                    // 执行工具调用
                    var toolResults = await ExecuteToolCallsAsync(
                        context.SessionId,
                        toolCalls,
                        context.Variables,
                        cancellationToken);
                    
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
                        return AgentResult.Success(
                            textContent,
                            messages,
                            totalTokenUsage,
                            iteration + 1);
                    }
                }
            }
            
            // 达到最大迭代次数
            return AgentResult.Failure(
                new AgentError("MAX_ITERATIONS", "Reached maximum iterations without completion"),
                messages,
                totalTokenUsage);
        }
        catch (OperationCanceledException)
        {
            return AgentResult.Failure(
                new AgentError("CANCELLED", "Operation was cancelled"),
                messages,
                totalTokenUsage,
                isRetryable: false);
        }
        catch (Exception ex)
        {
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
        List<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var chatOptions = new ChatOptions
        {
            Temperature = (float)_options.Temperature,
            MaxOutputTokens = _options.MaxTokens,
            Tools = _options.Tools.Select(ConvertToAITool).ToList()
        };
        
        var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        
        var tokenUsage = new TokenUsage(
            (int)(response.Usage?.InputTokenCount ?? 0),
            (int)(response.Usage?.OutputTokenCount ?? 0));
        
        // 返回最后一条消息（通常是 Assistant 的响应）
        var lastMessage = response.Messages.LastOrDefault() 
                          ?? new ChatMessage(ChatRole.Assistant, string.Empty);
        
        return (lastMessage, tokenUsage);
    }
    
    private static AITool ConvertToAITool(ITool tool)
    {
        return AIFunctionFactory.Create(
            async (JsonElement parameters, CancellationToken ct) =>
            {
                var context = new ToolExecutionContext
                {
                    Parameters = parameters,
                    RawArguments = parameters.GetRawText()
                };
                var result = await tool.ExecuteAsync(context, ct);
                return result.Content;
            },
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
                    $"Tool '{toolCall.Name}' not found. Available tools: {string.Join(", ", _options.Tools.Select(t => t.Name))}",
                    "TOOL_NOT_FOUND");
            }
            else
            {
                // 执行工具
                var sw = Stopwatch.StartNew();
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
                sw.Stop();
                result = result with { Duration = sw.Elapsed };
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
