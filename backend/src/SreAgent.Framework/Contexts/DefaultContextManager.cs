using System.Text.Json;
using Microsoft.Extensions.AI;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Contexts;

/// <summary>
/// 默认上下文管理器实现
/// 作为对话上下文的唯一负责人，采用充血模型设计
/// 负责消息管理、Token 统计和自动剪枝
/// </summary>
public class DefaultContextManager : IContextManager
{
    private readonly List<Message> _messages = [];
    private readonly ITokenEstimator _tokenEstimator;
    private readonly ContextManagerOptions _options;
    private readonly IChatMessageBuilder _defaultBuilder;
    private Message? _systemMessage;
    private readonly object _lock = new();

    // Token 管理
    private int _maxTokens;
    private int _reservedTokens;
    private TokenUsage _totalTokenUsage = new();

    public Guid SessionId { get; private set; } = Guid.NewGuid();

    public DefaultContextManager(
        ITokenEstimator tokenEstimator,
        ContextManagerOptions? options = null,
        IChatMessageBuilder? defaultBuilder = null)
    {
        _tokenEstimator = tokenEstimator;
        _options = options ?? new ContextManagerOptions();
        _defaultBuilder = defaultBuilder ?? DefaultChatMessageBuilder.Instance;
    }

    public DefaultContextManager(
        Guid sessionId,
        ITokenEstimator tokenEstimator,
        ContextManagerOptions? options = null,
        IChatMessageBuilder? defaultBuilder = null)
        : this(tokenEstimator, options, defaultBuilder)
    {
        SessionId = sessionId;
    }

    #region 基础属性

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

    public TokenUsage TotalTokenUsage
    {
        get
        {
            lock (_lock)
            {
                return _totalTokenUsage;
            }
        }
    }

    #endregion

    #region Token 管理

    public void ConfigureTokenLimit(int maxTokens, int reservedTokens = 0)
    {
        lock (_lock)
        {
            _maxTokens = maxTokens;
            _reservedTokens = reservedTokens;
        }
    }

    public void RecordTokenUsage(TokenUsage usage)
    {
        lock (_lock)
        {
            _totalTokenUsage += usage;
        }
    }

    /// <summary>
    /// 检查并执行剪枝（如果需要）
    /// </summary>
    private void TrimIfNeeded()
    {
        // 未配置 Token 限制或剪枝器，跳过
        if (_maxTokens <= 0 || _options.Trimmer == null) return;

        var availableTokens = _maxTokens - _reservedTokens;
        if (EstimatedTokenCount <= availableTokens) return;

        // 计算剪枝目标
        var targetTokens = (int)(availableTokens * _options.TrimTargetRatio);
        _options.Trimmer.Trim(this, targetTokens);
    }

    #endregion

    #region 基础消息操作

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

    #endregion

    #region 充血模型 - 语义化消息添加

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

    public void AddUserMessage(string input)
    {
        AddMessage(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Text = input }]
        });
    }

    public void AddAssistantMessage(ChatMessage response, string? agentId = null)
    {
        var message = ConvertFromChatMessage(response);
        message.Metadata.AgentId = agentId;
        AddMessage(message);
    }

    public void AddAssistantMessage(ChatMessage response, TokenUsage tokenUsage, string? agentId = null)
    {
        AddAssistantMessage(response, agentId);
        RecordTokenUsage(tokenUsage);
    }

    public void AddToolResultMessage(IReadOnlyList<(string CallId, string ToolName, ToolResult Result)> toolResults)
    {
        var parts = toolResults.Select(tr => (MessagePart)new ToolResultPart
        {
            ToolCallId = tr.CallId,
            ToolName = tr.ToolName,
            IsSuccess = tr.Result.IsSuccess,
            Content = tr.Result.Content
        }).ToList();

        AddMessage(new Message
        {
            Role = MessageRole.Tool,
            Parts = parts
        });
    }

    public void AddHistoryMessages(IEnumerable<ChatMessage> chatMessages)
    {
        foreach (var chatMessage in chatMessages)
        {
            AddMessage(ConvertFromChatMessage(chatMessage));
        }
    }

    #endregion

    #region 消息输出 - 用于 LLM 调用

    public IReadOnlyList<ChatMessage> GetChatMessages()
    {
        return GetChatMessages(_defaultBuilder);
    }

    public IReadOnlyList<ChatMessage> GetChatMessages(IChatMessageBuilder builder)
    {
        lock (_lock)
        {
            // 自动剪枝
            TrimIfNeeded();

            // 构建消息列表
            var messages = BuildMessageList();
            return builder.Build(messages);
        }
    }

    #endregion

    #region 内部方法

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

    private static Message ConvertFromChatMessage(ChatMessage chatMessage)
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

    #endregion

    #region 快照 - 用于持久化和恢复

    public ContextSnapshot ExportSnapshot(Guid sessionId, Dictionary<string, object>? metadata = null)
    {
        lock (_lock)
        {
            var systemMessageText = _systemMessage?.Parts
                .OfType<TextPart>()
                .FirstOrDefault()?.Text;

            return new ContextSnapshot
            {
                SessionId = sessionId,
                SystemMessage = systemMessageText,
                Messages = _messages.ToList(),
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = _messages.Count > 0 ? _messages.Max(m => m.CreatedAt) : null,
                EstimatedTokenCount = EstimatedTokenCount,
                Metadata = metadata ?? new Dictionary<string, object>(),
                Summary = GenerateSummaryInternal(200)
            };
        }
    }

    public void RestoreFromSnapshot(ContextSnapshot snapshot)
    {
        if (!snapshot.IsValid())
        {
            throw new ArgumentException("Invalid snapshot", nameof(snapshot));
        }

        lock (_lock)
        {
            SessionId = snapshot.SessionId;
            _messages.Clear();
            _systemMessage = null;

            if (!string.IsNullOrEmpty(snapshot.SystemMessage))
            {
                SetSystemMessage(snapshot.SystemMessage);
            }

            foreach (var message in snapshot.Messages)
            {
                _messages.Add(message);
            }
        }
    }

    public string GenerateSummary(int maxTokens = 500)
    {
        lock (_lock)
        {
            return GenerateSummaryInternal(maxTokens);
        }
    }

    private string GenerateSummaryInternal(int maxTokens)
    {
        if (_messages.Count == 0)
        {
            return "No conversation history.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Conversation summary ({_messages.Count} messages):");

        var currentTokens = _tokenEstimator.EstimateTokens(sb.ToString());

        foreach (var message in _messages.TakeLast(10))
        {
            var roleText = message.Role switch
            {
                MessageRole.User => "User",
                MessageRole.Assistant => "Assistant",
                MessageRole.Tool => "Tool",
                _ => message.Role.ToString()
            };

            var contentPreview = GetMessagePreview(message, 100);
            var line = $"- [{roleText}]: {contentPreview}";
            var lineTokens = _tokenEstimator.EstimateTokens(line);

            if (currentTokens + lineTokens > maxTokens)
            {
                sb.AppendLine("... (more messages omitted)");
                break;
            }

            sb.AppendLine(line);
            currentTokens += lineTokens;
        }

        return sb.ToString();
    }

    private static string GetMessagePreview(Message message, int maxLength)
    {
        var textParts = message.Parts.OfType<TextPart>().ToList();
        if (textParts.Count > 0)
        {
            var text = textParts[0].Text;
            return text.Length > maxLength ? text[..maxLength] + "..." : text;
        }

        var toolCalls = message.Parts.OfType<ToolCallPart>().ToList();
        if (toolCalls.Count > 0)
        {
            return $"[Tool call: {string.Join(", ", toolCalls.Select(t => t.Name))}]";
        }

        var toolResults = message.Parts.OfType<ToolResultPart>().ToList();
        if (toolResults.Count > 0)
        {
            return $"[Tool results: {string.Join(", ", toolResults.Select(t => t.ToolName))}]";
        }

        return "[Empty message]";
    }

    #endregion

    #region 工厂方法

    /// <summary>
    /// 从快照创建新的 ContextManager 实例
    /// </summary>
    public static DefaultContextManager FromSnapshot(
        ContextSnapshot snapshot,
        ITokenEstimator tokenEstimator,
        ContextManagerOptions? options = null,
        IChatMessageBuilder? defaultBuilder = null)
    {
        var manager = new DefaultContextManager(tokenEstimator, options, defaultBuilder);
        manager.RestoreFromSnapshot(snapshot);
        return manager;
    }

    /// <summary>
    /// 开始新对话
    /// </summary>
    public static DefaultContextManager StartNew(
        string userInput,
        string? systemPrompt,
        ITokenEstimator tokenEstimator,
        Guid? sessionId = null,
        ContextManagerOptions? options = null)
    {
        var manager = sessionId.HasValue
            ? new DefaultContextManager(sessionId.Value, tokenEstimator, options)
            : new DefaultContextManager(tokenEstimator, options);

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            manager.SetSystemMessage(systemPrompt);
        }
        manager.AddUserMessage(userInput);

        return manager;
    }

    /// <summary>
    /// 在现有上下文上继续对话
    /// </summary>
    public static IContextManager Continue(IContextManager existingContext, string userInput)
    {
        existingContext.AddUserMessage(userInput);
        return existingContext;
    }

    #endregion
}
