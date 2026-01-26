using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SreAgent.Framework.Contexts.Trimmers;

/// <summary>
/// 基于优先级的剪枝器
/// 优先保留高优先级消息，同优先级按时间倒序
/// </summary>
public class PriorityBasedContextTrimmer : IContextTrimmer
{
    private readonly ILogger<PriorityBasedContextTrimmer> _logger;
    
    public string Name => "PriorityBased";
    
    public PriorityBasedContextTrimmer(ILogger<PriorityBasedContextTrimmer>? logger = null)
    {
        _logger = logger ?? NullLogger<PriorityBasedContextTrimmer>.Instance;
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
        
        // 按优先级排序（高优先级在前），同优先级按时间倒序
        var sorted = allMessages
            .OrderByDescending(m => m.Metadata.Priority)
            .ThenByDescending(m => m.CreatedAt)
            .ToList();
        
        var messagesToKeep = new List<Message>();
        var currentTokens = 0;
        
        foreach (var msg in sorted)
        {
            if (currentTokens + msg.Metadata.EstimatedTokens <= targetTokens || !msg.Metadata.IsDeletable)
            {
                messagesToKeep.Add(msg);
                currentTokens += msg.Metadata.EstimatedTokens;
            }
        }
        
        // 按时间重新排序，保持对话顺序
        messagesToKeep = messagesToKeep.OrderBy(m => m.CreatedAt).ToList();
        
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
