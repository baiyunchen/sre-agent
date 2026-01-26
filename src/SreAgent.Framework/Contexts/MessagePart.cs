namespace SreAgent.Framework.Contexts;

/// <summary>
/// Part 基类 - 消息的组成部分
/// </summary>
public abstract class MessagePart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public abstract MessagePartType Type { get; }
}

/// <summary>
/// 消息部分类型
/// </summary>
public enum MessagePartType
{
    Text,
    ToolCall,
    ToolResult,
    Image,
    Error
}

/// <summary>
/// 文本部分
/// </summary>
public class TextPart : MessagePart
{
    public override MessagePartType Type => MessagePartType.Text;
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// 工具调用部分
/// </summary>
public class ToolCallPart : MessagePart
{
    public override MessagePartType Type => MessagePartType.ToolCall;
    public string ToolCallId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Arguments { get; init; } = "{}";
}

/// <summary>
/// 工具结果部分
/// </summary>
public class ToolResultPart : MessagePart
{
    public override MessagePartType Type => MessagePartType.ToolResult;
    public string ToolCallId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// 图片部分
/// </summary>
public class ImagePart : MessagePart
{
    public override MessagePartType Type => MessagePartType.Image;
    public string MimeType { get; init; } = "image/png";
    public byte[]? Data { get; init; }
    public string? Url { get; init; }
}

/// <summary>
/// 错误部分
/// </summary>
public class ErrorPart : MessagePart
{
    public override MessagePartType Type => MessagePartType.Error;
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
