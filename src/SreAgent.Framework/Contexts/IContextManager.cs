using Microsoft.Extensions.AI;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Contexts;

/// <summary>
/// 上下文管理器接口
/// 作为对话上下文的唯一负责人，管理所有消息、Token 统计和自动剪枝
/// Framework 提供默认实现，业务层可扩展
/// </summary>
public interface IContextManager
{
    /// <summary>会话 ID</summary>
    Guid SessionId { get; }

    #region 基础消息操作

    /// <summary>添加消息到上下文</summary>
    void AddMessage(Message message);

    /// <summary>批量添加消息</summary>
    void AddMessages(IEnumerable<Message> messages);

    /// <summary>获取所有消息（内部格式）</summary>
    IReadOnlyList<Message> GetMessages();

    /// <summary>当前上下文的估算 Token 数</summary>
    int EstimatedTokenCount { get; }

    /// <summary>清空上下文（保留 System 消息）</summary>
    void Clear();

    #endregion

    #region Token 管理

    /// <summary>
    /// 配置 Token 限制（用于自动剪枝）
    /// </summary>
    /// <param name="maxTokens">上下文窗口最大 Token 数</param>
    /// <param name="reservedTokens">预留 Token 数（如工具定义占用）</param>
    void ConfigureTokenLimit(int maxTokens, int reservedTokens = 0);

    /// <summary>
    /// 记录 LLM 返回的 Token 使用情况
    /// </summary>
    void RecordTokenUsage(TokenUsage usage);

    /// <summary>累计 Token 使用量</summary>
    TokenUsage TotalTokenUsage { get; }

    #endregion

    #region 充血模型 - 语义化消息添加

    /// <summary>设置 System 消息（会替换已有的）</summary>
    void SetSystemMessage(string content);

    /// <summary>获取 System 消息</summary>
    Message? GetSystemMessage();

    /// <summary>添加用户输入消息</summary>
    void AddUserMessage(string input);

    /// <summary>
    /// 添加 Assistant 响应消息
    /// </summary>
    /// <param name="response">LLM 返回的 ChatMessage</param>
    /// <param name="agentId">Agent 标识（可选）</param>
    void AddAssistantMessage(ChatMessage response, string? agentId = null);

    /// <summary>
    /// 添加 Assistant 响应消息并记录 Token 使用
    /// </summary>
    /// <param name="response">LLM 返回的 ChatMessage</param>
    /// <param name="tokenUsage">本次调用的 Token 使用量</param>
    /// <param name="agentId">Agent 标识（可选）</param>
    void AddAssistantMessage(ChatMessage response, TokenUsage tokenUsage, string? agentId = null);

    /// <summary>添加工具执行结果消息</summary>
    void AddToolResultMessage(IReadOnlyList<(string CallId, string ToolName, ToolResult Result)> toolResults);

    /// <summary>从 ChatMessage 列表批量添加历史消息</summary>
    void AddHistoryMessages(IEnumerable<ChatMessage> chatMessages);

    #endregion

    #region 消息输出 - 用于 LLM 调用

    /// <summary>
    /// 获取用于发送给 LLM 的 ChatMessage 列表
    /// 内部会自动进行剪枝（如果配置了 Token 限制）
    /// </summary>
    IReadOnlyList<ChatMessage> GetChatMessages();

    /// <summary>
    /// 获取用于发送给 LLM 的 ChatMessage 列表
    /// 使用指定的构建策略，内部会自动进行剪枝
    /// </summary>
    IReadOnlyList<ChatMessage> GetChatMessages(IChatMessageBuilder builder);

    #endregion

    #region 快照 - 用于持久化和恢复

    /// <summary>导出上下文快照</summary>
    ContextSnapshot ExportSnapshot(Guid sessionId, Dictionary<string, object>? metadata = null);

    /// <summary>从快照恢复上下文</summary>
    void RestoreFromSnapshot(ContextSnapshot snapshot);

    /// <summary>生成当前上下文的摘要</summary>
    string GenerateSummary(int maxTokens = 500);

    #endregion
}

/// <summary>
/// 上下文管理器配置选项
/// </summary>
public class ContextManagerOptions
{
    /// <summary>是否自动压缩长工具结果</summary>
    public bool AutoCompressToolResults { get; set; } = true;

    /// <summary>工具结果压缩阈值（Token 数）</summary>
    public int ToolResultCompressThreshold { get; set; } = 1000;

    /// <summary>
    /// 上下文剪枝器（可选）
    /// 如果设置，会在 GetChatMessages() 时自动剪枝
    /// </summary>
    public IContextTrimmer? Trimmer { get; set; }

    /// <summary>
    /// 剪枝目标比例（相对于可用 Token 限制）
    /// 默认 0.8，即剪枝后保留 80% 的可用空间
    /// </summary>
    public double TrimTargetRatio { get; set; } = 0.8;
}

/// <summary>
/// ChatMessage 构建器接口
/// 定义如何从内部 Message 列表构建发送给 LLM 的 ChatMessage 列表
/// 允许实现不同的构建策略（如：过滤、转换、增强等）
/// </summary>
public interface IChatMessageBuilder
{
    /// <summary>
    /// 从内部消息构建 ChatMessage 列表
    /// </summary>
    /// <param name="messages">内部消息列表（包含 System 消息）</param>
    /// <returns>用于 LLM 调用的 ChatMessage 列表</returns>
    IReadOnlyList<ChatMessage> Build(IReadOnlyList<Message> messages);
}
