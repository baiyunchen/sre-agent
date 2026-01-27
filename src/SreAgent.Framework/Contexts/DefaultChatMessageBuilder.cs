using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SreAgent.Framework.Contexts;

/// <summary>
/// 默认的 ChatMessage 构建器
/// 将内部 Message 列表直接转换为 ChatMessage 列表
/// </summary>
public class DefaultChatMessageBuilder : IChatMessageBuilder
{
    /// <summary>
    /// 单例实例
    /// </summary>
    public static DefaultChatMessageBuilder Instance { get; } = new();

    public IReadOnlyList<ChatMessage> Build(IReadOnlyList<Message> messages)
    {
        var result = new List<ChatMessage>(messages.Count);

        foreach (var message in messages)
        {
            result.Add(ConvertToChatMessage(message));
        }

        return result;
    }

    private static ChatMessage ConvertToChatMessage(Message message)
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
            var content = ConvertPartToContent(part);
            if (content != null)
            {
                contents.Add(content);
            }
        }

        return new ChatMessage(chatRole, contents);
    }

    private static AIContent? ConvertPartToContent(MessagePart part)
    {
        return part switch
        {
            TextPart textPart => new TextContent(textPart.Text),
            ToolCallPart toolCallPart => CreateFunctionCallContent(toolCallPart),
            ToolResultPart toolResultPart => new FunctionResultContent(toolResultPart.ToolCallId, toolResultPart.Content),
            _ => null
        };
    }

    private static FunctionCallContent CreateFunctionCallContent(ToolCallPart toolCallPart)
    {
        var args = string.IsNullOrEmpty(toolCallPart.Arguments)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCallPart.Arguments);
        return new FunctionCallContent(toolCallPart.ToolCallId, toolCallPart.Name, args);
    }
}
