namespace SreAgent.Framework.Contexts;

/// <summary>
/// 默认上下文管理器实现
/// </summary>
public class DefaultContextManager : IContextManager
{
    private readonly List<Message> _messages = [];
    private readonly ITokenEstimator _tokenEstimator;
    private readonly ContextManagerOptions _options;
    private Message? _systemMessage;
    private readonly object _lock = new();
    
    public DefaultContextManager(
        ITokenEstimator tokenEstimator,
        ContextManagerOptions? options = null)
    {
        _tokenEstimator = tokenEstimator;
        _options = options ?? new ContextManagerOptions();
    }
    
    public int EstimatedTokenCount
    {
        get
        {
            lock (_lock)
            {
                var total = _systemMessage?.Metadata.EstimatedTokens ?? 0;
                return total + _messages.Sum(m => m.Metadata.EstimatedTokens);
            }
        }
    }
    
    public void SetSystemMessage(string content)
    {
        lock (_lock)
        {
            _systemMessage = new Message
            {
                Role = MessageRole.System,
                Parts = [new TextPart { Text = content }],
                Metadata = new MessageMetadata
                {
                    IsDeletable = false,
                    Priority = MessagePriority.Critical,
                    EstimatedTokens = _tokenEstimator.EstimateTokens(content)
                }
            };
        }
    }
    
    public Message? GetSystemMessage()
    {
        lock (_lock)
        {
            return _systemMessage;
        }
    }
    
    public void AddMessage(Message message)
    {
        // 计算 Token 数
        message.Metadata.EstimatedTokens = _tokenEstimator.EstimateTokens(message);
        
        // 自动压缩长工具结果
        if (_options.AutoCompressToolResults)
        {
            CompressToolResultsIfNeeded(message);
        }
        
        lock (_lock)
        {
            _messages.Add(message);
        }
    }
    
    public void AddMessages(IEnumerable<Message> messages)
    {
        foreach (var message in messages)
        {
            AddMessage(message);
        }
    }
    
    public IReadOnlyList<Message> GetMessages()
    {
        lock (_lock)
        {
            return BuildMessageList();
        }
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
            // 注意：不清除 System 消息
        }
    }
    
    private List<Message> BuildMessageList()
    {
        var result = new List<Message>();
        if (_systemMessage != null)
        {
            result.Add(_systemMessage);
        }
        result.AddRange(_messages);
        return result;
    }
    
    private void CompressToolResultsIfNeeded(Message message)
    {
        var toolResultParts = message.Parts.OfType<ToolResultPart>().ToList();
        
        foreach (var part in toolResultParts)
        {
            var tokens = _tokenEstimator.EstimateTokens(part.Content);
            if (tokens > _options.ToolResultCompressThreshold)
            {
                // 压缩长结果
                var compressed = CompressContent(part.Content, _options.ToolResultCompressThreshold);
                var index = message.Parts.IndexOf(part);
                message.Parts[index] = new ToolResultPart
                {
                    Id = part.Id,
                    ToolCallId = part.ToolCallId,
                    ToolName = part.ToolName,
                    IsSuccess = part.IsSuccess,
                    Content = compressed
                };
            }
        }
    }
    
    private string CompressContent(string content, int targetTokens)
    {
        // 简单的截断策略
        var lines = content.Split('\n');
        var result = new List<string>();
        var currentTokens = 0;
        
        foreach (var line in lines)
        {
            var lineTokens = _tokenEstimator.EstimateTokens(line);
            if (currentTokens + lineTokens > targetTokens)
            {
                result.Add("... [content truncated] ...");
                break;
            }
            result.Add(line);
            currentTokens += lineTokens;
        }
        
        return string.Join('\n', result);
    }
}
