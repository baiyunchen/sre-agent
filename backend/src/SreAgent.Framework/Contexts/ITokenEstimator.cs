namespace SreAgent.Framework.Contexts;

/// <summary>
/// Token 估算器接口
/// </summary>
public interface ITokenEstimator
{
    /// <summary>估算文本的 Token 数</summary>
    int EstimateTokens(string text);
    
    /// <summary>估算消息的 Token 数</summary>
    int EstimateTokens(Message message);
    
    /// <summary>估算多条消息的 Token 数</summary>
    int EstimateTokens(IEnumerable<Message> messages);
}

/// <summary>
/// 简单的字符估算器
/// 基于字符数估算 Token 数，适用于快速估算
/// </summary>
public class SimpleTokenEstimator : ITokenEstimator
{
    private const double CharsPerToken = 4.0;
    private const int MessageOverhead = 4; // 消息格式化开销
    
    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }
    
    public int EstimateTokens(Message message)
    {
        var contentTokens = 0;
        
        foreach (var part in message.Parts)
        {
            contentTokens += part switch
            {
                TextPart text => EstimateTokens(text.Text),
                ToolCallPart toolCall => EstimateTokens(toolCall.Name) + EstimateTokens(toolCall.Arguments),
                ToolResultPart toolResult => EstimateTokens(toolResult.Content),
                ErrorPart error => EstimateTokens(error.ErrorMessage),
                _ => 0
            };
        }
        
        return MessageOverhead + contentTokens;
    }
    
    public int EstimateTokens(IEnumerable<Message> messages)
    {
        return messages.Sum(EstimateTokens);
    }
}
