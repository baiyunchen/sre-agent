using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SreAgent.Framework.Contexts.Trimmers;

/// <summary>
/// 移除最早消息的剪枝器（默认策略）
/// 从后向前保留消息，优先保留最新的对话内容
/// </summary>
public class RemoveOldestContextTrimmer : IContextTrimmer
{
    private readonly ILogger<RemoveOldestContextTrimmer> _logger;
    
    public string Name => "RemoveOldest";
    
    public RemoveOldestContextTrimmer(ILogger<RemoveOldestContextTrimmer>? logger = null)
    {
        _logger = logger ?? NullLogger<RemoveOldestContextTrimmer>.Instance;
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
        var messagesRemoved = 0;
        
        // 始终保留 System 消息
        var systemMsg = contextManager.GetSystemMessage();
        if (systemMsg != null)
        {
            messagesToKeep.Add(systemMsg);
            currentTokens += systemMsg.Metadata.EstimatedTokens;
        }
        
        // 从后向前遍历，保留最新的消息
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
            else if (!msg.Metadata.IsDeletable)
            {
                // 不可删除的消息强制保留
                messagesToKeep.Insert(systemMsg != null ? 1 : 0, msg);
                currentTokens += msg.Metadata.EstimatedTokens;
            }
            else
            {
                messagesRemoved++;
            }
        }
        
        // 重建上下文
        RebuildContext(contextManager, messagesToKeep);
        
        _logger.LogInformation(
            "{Strategy} trimming completed. Before: {Before}, After: {After}, Removed: {Removed}",
            Name, tokensBefore, currentTokens, messagesRemoved);
        
        return TrimResult.Success(tokensBefore, currentTokens, messagesRemoved, Name);
    }
    
    private static void RebuildContext(IContextManager contextManager, List<Message> messagesToKeep)
    {
        // 保存 System 消息
        var systemMsg = contextManager.GetSystemMessage();
        
        // 清空并重建
        contextManager.Clear();
        
        // 添加非 System 消息
        var nonSystemMessages = messagesToKeep.Where(m => m.Role != MessageRole.System);
        contextManager.AddMessages(nonSystemMessages);
    }
}
