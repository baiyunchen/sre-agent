using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using SreAgent.Application.Tools.KnowledgeBase.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Prompts;
using SreAgent.Framework.Results;

namespace SreAgent.Application.Tools.KnowledgeBase;

/// <summary>
/// Knowledge Base 查询工具
/// 用于从 AWS Bedrock Knowledge Base 中检索 SRE Playbook 信息
/// </summary>
public class KnowledgeBaseQueryTool : ToolBase<KnowledgeBaseQueryParams>
{
    private static readonly Lazy<string> PromptContent = PromptLoader.CreateLazy<KnowledgeBaseQueryTool>(
        "KnowledgeBaseQueryPrompt.txt",
        "Search the SRE Knowledge Base for playbooks and troubleshooting guides.");

    private readonly IKnowledgeBaseService _knowledgeBaseService;

    public KnowledgeBaseQueryTool(IKnowledgeBaseService knowledgeBaseService)
    {
        _knowledgeBaseService = knowledgeBaseService;
    }

    public override string Name => "knowledge_base_query";

    public override string Summary => "Search SRE playbooks and troubleshooting guides in Knowledge Base";

    public override string Description => PromptContent.Value;

    public override string Category => "Knowledge Management";

    protected override string ReturnDescription =>
        "Returns relevant playbook sections with troubleshooting steps, log queries, and resolution guidance";

    protected override async Task<ToolResult> ExecuteAsync(
        KnowledgeBaseQueryParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var result = await _knowledgeBaseService.QueryAsync(
            parameters.Query,
            parameters.NumberOfResults ?? 5,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToolResult.Failure(
                result.ErrorMessage ?? "Knowledge Base 查询失败",
                "KNOWLEDGE_BASE_QUERY_FAILED",
                isRetryable: true);
        }

        if (result.Documents.Count == 0)
        {
            return ToolResult.Success(
                FormatNoResults(parameters.Query),
                new { documents = Array.Empty<object>(), count = 0 });
        }

        return ToolResult.Success(
            FormatResults(result, parameters.Query),
            new
            {
                documents = result.Documents.Select(d => new
                {
                    content = d.Content,
                    score = d.Score,
                    sourceUri = d.SourceUri,
                    title = d.Title,
                    metadata = d.Metadata
                }),
                count = result.Documents.Count
            });
    }

    private static string FormatNoResults(string query)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📚 Knowledge Base 查询结果");
        sb.AppendLine("─".PadRight(50, '─'));
        sb.AppendLine($"查询: {query}");
        sb.AppendLine();
        sb.AppendLine("⚠️ 未找到相关的 Playbook 或文档");
        sb.AppendLine();
        sb.AppendLine("建议:");
        sb.AppendLine("- 尝试使用更通用的关键词");
        sb.AppendLine("- 检查服务名称是否正确");
        sb.AppendLine("- 尝试搜索错误类型而非具体错误信息");
        return sb.ToString();
    }

    private static string FormatResults(KnowledgeBaseQueryResult result, string query)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📚 Knowledge Base 查询结果");
        sb.AppendLine("─".PadRight(50, '─'));
        sb.AppendLine($"查询: {query}");
        sb.AppendLine($"找到 {result.Documents.Count} 个相关文档");
        sb.AppendLine("─".PadRight(50, '─'));
        sb.AppendLine();

        var index = 1;
        foreach (var doc in result.Documents)
        {
            sb.AppendLine($"### 文档 {index} (相关度: {doc.Score:P1})");
            
            if (!string.IsNullOrEmpty(doc.Title))
            {
                sb.AppendLine($"**标题**: {doc.Title}");
            }
            
            if (!string.IsNullOrEmpty(doc.SourceUri))
            {
                sb.AppendLine($"**来源**: {doc.SourceUri}");
            }
            
            sb.AppendLine();
            sb.AppendLine("**内容**:");
            sb.AppendLine(doc.Content);
            sb.AppendLine();
            sb.AppendLine("─".PadRight(40, '─'));
            sb.AppendLine();
            
            index++;
        }

        return sb.ToString();
    }
}

#region Parameters

/// <summary>
/// Knowledge Base 查询参数
/// </summary>
public class KnowledgeBaseQueryParams
{
    /// <summary>
    /// 查询文本
    /// </summary>
    [Required]
    [Description("The search query to find relevant playbooks. Include service name, alert name, or error type for best results. Examples: 'order-service 5xx errors', 'inventory-service-lambda-errors', 'DLQ messages notification service'")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 返回结果数量
    /// </summary>
    [Description("Number of results to return. Default is 5, maximum is 10. More results provide broader context but may include less relevant documents.")]
    [Range(1, 10)]
    public int? NumberOfResults { get; set; }
}

#endregion
