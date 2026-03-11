using System.Text.Json;
using Microsoft.Extensions.AI;
using SreAgent.Framework.Contexts;

namespace SreAgent.Framework.Agents;

/// <summary>
/// 消息转换器 - 提供 ChatMessage 与内部 Message 之间的静态转换方法
/// 注意：大部分转换逻辑已迁移到 ContextManager 和 DefaultChatMessageBuilder
/// 此类保留用于需要独立转换的场景
/// </summary>
public static class MessageConverter
{
    /// <summary>
    /// 将 Microsoft.Extensions.AI.ChatMessage 转换为内部 Message
    /// </summary>
    public static Message FromChatMessage(ChatMessage chatMessage)
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
                        ToolName = string.Empty,
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

    /// <summary>
    /// 将内部 Message 转换为 Microsoft.Extensions.AI.ChatMessage
    /// </summary>
    public static ChatMessage ToChatMessage(Message message)
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

        return new ChatMessage(chatRole, contents);
    }
}
