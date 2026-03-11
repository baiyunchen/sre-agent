namespace SreAgent.Application.Tools.KnowledgeBase.Services;

/// <summary>
/// AWS Bedrock Knowledge Base 服务接口
/// </summary>
public interface IKnowledgeBaseService
{
    /// <summary>
    /// 查询 Knowledge Base
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="numberOfResults">返回结果数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果</returns>
    Task<KnowledgeBaseQueryResult> QueryAsync(
        string query,
        int numberOfResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用检索增强生成 (RAG) 查询 Knowledge Base
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="numberOfResults">检索结果数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>RAG 响应结果</returns>
    Task<KnowledgeBaseRagResult> RetrieveAndGenerateAsync(
        string query,
        int numberOfResults = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Knowledge Base 查询结果
/// </summary>
public class KnowledgeBaseQueryResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 检索到的文档片段
    /// </summary>
    public List<RetrievedDocument> Documents { get; set; } = new();

    public static KnowledgeBaseQueryResult Success(List<RetrievedDocument> documents)
    {
        return new KnowledgeBaseQueryResult
        {
            IsSuccess = true,
            Documents = documents
        };
    }

    public static KnowledgeBaseQueryResult Failure(string errorMessage)
    {
        return new KnowledgeBaseQueryResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// 检索到的文档
/// </summary>
public class RetrievedDocument
{
    /// <summary>
    /// 文档内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相关性分数 (0-1)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// 文档来源 URI
    /// </summary>
    public string? SourceUri { get; set; }

    /// <summary>
    /// 文档标题
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Knowledge Base RAG 结果
/// </summary>
public class KnowledgeBaseRagResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 生成的响应文本
    /// </summary>
    public string? GeneratedResponse { get; set; }

    /// <summary>
    /// 引用的文档
    /// </summary>
    public List<RetrievedDocument> Citations { get; set; } = new();

    public static KnowledgeBaseRagResult Success(string response, List<RetrievedDocument> citations)
    {
        return new KnowledgeBaseRagResult
        {
            IsSuccess = true,
            GeneratedResponse = response,
            Citations = citations
        };
    }

    public static KnowledgeBaseRagResult Failure(string errorMessage)
    {
        return new KnowledgeBaseRagResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
