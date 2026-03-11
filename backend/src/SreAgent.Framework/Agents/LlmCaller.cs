using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Agents;

/// <summary>
/// LLM 调用器 - 负责调用 LLM 并处理响应
/// </summary>
public class LlmCaller
{
    private readonly ILogger _logger;

    public LlmCaller(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// 调用 LLM 并返回响应
    /// </summary>
    /// <param name="chatClient">Chat 客户端</param>
    /// <param name="messages">消息列表</param>
    /// <param name="tools">可用的工具列表</param>
    /// <param name="temperature">温度参数</param>
    /// <param name="maxTokens">最大输出 Token 数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>LLM 响应和 Token 使用量</returns>
    public async Task<(ChatMessage Response, TokenUsage TokenUsage)> CallAsync(
        IChatClient chatClient,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ITool> tools,
        double temperature,
        int? maxTokens,
        CancellationToken cancellationToken = default)
    {
        var chatOptions = new ChatOptions
        {
            Temperature = (float)temperature,
            MaxOutputTokens = maxTokens,
            Tools = ToolExecutor.ToAITools(tools)
        };

        var response = await chatClient.GetResponseAsync(messages.ToList(), chatOptions, cancellationToken);

        LogResponse(response);

        var tokenUsage = new TokenUsage(
            (int)(response.Usage?.InputTokenCount ?? 0),
            (int)(response.Usage?.OutputTokenCount ?? 0));

        // 返回最后一条消息（通常是 Assistant 的响应）
        var lastMessage = response.Messages.LastOrDefault()
                          ?? new ChatMessage(ChatRole.Assistant, string.Empty);

        return (lastMessage, tokenUsage);
    }

    /// <summary>
    /// 记录 LLM 响应日志
    /// </summary>
    private void LogResponse(ChatResponse response)
    {
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
    }
}
