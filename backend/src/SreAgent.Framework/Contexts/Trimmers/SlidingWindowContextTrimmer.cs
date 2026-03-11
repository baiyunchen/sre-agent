using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SreAgent.Framework.Contexts.Trimmers;

/// <summary>
/// 滑动窗口剪枝器
/// 保留最近的连续消息，遇到超限立即停止
/// </summary>
public class SlidingWindowContextTrimmer : IContextTrimmer
{
    private readonly ILogger<SlidingWindowContextTrimmer> _logger;
    
    public string Name => "SlidingWindow";
    
    public SlidingWindowContextTrimmer(ILogger<SlidingWindowContextTrimmer>? logger = null)
    {
        _logger = logger ?? NullLogger<SlidingWindowContextTrimmer>.Instance;
    }
    
    public TrimResult Trim(IContextManager contextManager, int targetTokens)
    {
        var allMessages = contextManager.GetMessages();
        var tokensBefore = allMessages.Sum(m => m.Metadata.EstimatedTokens);
        
        if (tokensBefore <= targetTokens)
        {
            return TrimResult.NoTrimNeeded(tokensBefore, Name);
        }
        
        _logger.LogInformation(
            "Starting {Strategy} trimming. Current: {Current}, Target: {Target}",
            Name, tokensBefore, targetTokens);
        
        var messagesToKeep = new List<Message>();
        var currentTokens = 0;
        
        // 保留 System 消息
        var systemMsg = contextManager.GetSystemMessage();
        if (systemMsg != null)
        {
            messagesToKeep.Add(systemMsg);
            currentTokens += systemMsg.Metadata.EstimatedTokens;
        }
        
        // 从后向前，连续保留消息直到超限
        var nonSystemMessages = allMessages
            .Where(m => m.Role != MessageRole.System)
            .Reverse()
            .ToList();
        
        foreach (var msg in nonSystemMessages)
        {
            if (currentTokens + msg.Metadata.EstimatedTokens <= targetTokens)
            {
                messagesToKeep.Insert(systemMsg != null ? 1 : 0, msg);
                currentTokens += msg.Metadata.EstimatedTokens;
            }
            else
            {
                // 滑动窗口策略：遇到第一个超限消息就停止
                break;
            }
        }
        
        var messagesRemoved = allMessages.Count - messagesToKeep.Count;
        
        // 重建上下文
        RebuildContext(contextManager, messagesToKeep);
        
        _logger.LogInformation(
            "{Strategy} trimming completed. Before: {Before}, After: {After}, Removed: {Removed}",
            Name, tokensBefore, currentTokens, messagesRemoved);
        
        return TrimResult.Success(tokensBefore, currentTokens, messagesRemoved, Name);
    }
    
    private static void RebuildContext(IContextManager contextManager, List<Message> messagesToKeep)
    {
        // 清空并重建
        contextManager.Clear();
        
        // 添加非 System 消息
        var nonSystemMessages = messagesToKeep.Where(m => m.Role != MessageRole.System);
        contextManager.AddMessages(nonSystemMessages);
    }
}
