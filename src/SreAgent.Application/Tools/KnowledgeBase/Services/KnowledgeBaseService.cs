using Amazon;
using Amazon.BedrockAgentRuntime;
using Amazon.BedrockAgentRuntime.Model;
using Microsoft.Extensions.Logging;

namespace SreAgent.Application.Tools.KnowledgeBase.Services;

/// <summary>
/// AWS Bedrock Knowledge Base 服务实现
/// </summary>
public class KnowledgeBaseService : IKnowledgeBaseService, IDisposable
{
    private readonly AmazonBedrockAgentRuntimeClient _client;
    private readonly KnowledgeBaseServiceOptions _options;
    private readonly ILogger<KnowledgeBaseService>? _logger;

    public KnowledgeBaseService(
        KnowledgeBaseServiceOptions options,
        ILogger<KnowledgeBaseService>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _client = CreateClient();
    }

    private AmazonBedrockAgentRuntimeClient CreateClient()
    {
        var config = new AmazonBedrockAgentRuntimeConfig();

        if (!string.IsNullOrEmpty(_options.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);
        }

        if (!string.IsNullOrEmpty(_options.ServiceUrl))
        {
            config.ServiceURL = _options.ServiceUrl;
        }

        return new AmazonBedrockAgentRuntimeClient(config);
    }

    /// <inheritdoc />
    public async Task<KnowledgeBaseQueryResult> QueryAsync(
        string query,
        int numberOfResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.KnowledgeBaseId))
        {
            return KnowledgeBaseQueryResult.Failure("Knowledge Base ID 未配置");
        }

        try
        {
            _logger?.LogInformation(
                "查询 Knowledge Base: KnowledgeBaseId={KnowledgeBaseId}, Query={Query}",
                _options.KnowledgeBaseId, query);

            var request = new RetrieveRequest
            {
                KnowledgeBaseId = _options.KnowledgeBaseId,
                RetrievalQuery = new KnowledgeBaseQuery
                {
                    Text = query
                },
                RetrievalConfiguration = new KnowledgeBaseRetrievalConfiguration
                {
                    VectorSearchConfiguration = new KnowledgeBaseVectorSearchConfiguration
                    {
                        NumberOfResults = numberOfResults,
                        OverrideSearchType = SearchType.HYBRID
                    }
                }
            };

            var response = await _client.RetrieveAsync(request, cancellationToken);

            var documents = response.RetrievalResults
                .Select(r => new RetrievedDocument
                {
                    Content = r.Content?.Text ?? string.Empty,
                    Score = r.Score,
                    SourceUri = r.Location?.ConfluenceLocation?.Url 
                               ?? r.Location?.S3Location?.Uri 
                               ?? r.Location?.WebLocation?.Url,
                    Title = ExtractTitle(r),
                    Metadata = ExtractMetadata(r)
                })
                .ToList();

            _logger?.LogInformation(
                "Knowledge Base 查询完成，返回 {Count} 个结果",
                documents.Count);

            return KnowledgeBaseQueryResult.Success(documents);
        }
        catch (ResourceNotFoundException ex)
        {
            _logger?.LogWarning(ex, "Knowledge Base 不存在: {KnowledgeBaseId}", _options.KnowledgeBaseId);
            return KnowledgeBaseQueryResult.Failure($"Knowledge Base 不存在: {_options.KnowledgeBaseId}");
        }
        catch (ValidationException ex)
        {
            _logger?.LogWarning(ex, "请求验证失败");
            return KnowledgeBaseQueryResult.Failure($"请求验证失败: {ex.Message}");
        }
        catch (AmazonBedrockAgentRuntimeException ex)
        {
            _logger?.LogError(ex, "Knowledge Base 查询失败: {ErrorCode}", ex.ErrorCode);
            return KnowledgeBaseQueryResult.Failure($"Knowledge Base 查询失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Knowledge Base 查询发生未知错误");
            return KnowledgeBaseQueryResult.Failure($"查询失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<KnowledgeBaseRagResult> RetrieveAndGenerateAsync(
        string query,
        int numberOfResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.KnowledgeBaseId))
        {
            return KnowledgeBaseRagResult.Failure("Knowledge Base ID 未配置");
        }

        if (string.IsNullOrWhiteSpace(_options.FoundationModelArn))
        {
            return KnowledgeBaseRagResult.Failure("Foundation Model ARN 未配置");
        }

        try
        {
            _logger?.LogInformation(
                "RAG 查询 Knowledge Base: KnowledgeBaseId={KnowledgeBaseId}, Query={Query}",
                _options.KnowledgeBaseId, query);

            var request = new RetrieveAndGenerateRequest
            {
                Input = new RetrieveAndGenerateInput
                {
                    Text = query
                },
                RetrieveAndGenerateConfiguration = new RetrieveAndGenerateConfiguration
                {
                    Type = RetrieveAndGenerateType.KNOWLEDGE_BASE,
                    KnowledgeBaseConfiguration = new KnowledgeBaseRetrieveAndGenerateConfiguration
                    {
                        KnowledgeBaseId = _options.KnowledgeBaseId,
                        ModelArn = _options.FoundationModelArn,
                        RetrievalConfiguration = new KnowledgeBaseRetrievalConfiguration
                        {
                            VectorSearchConfiguration = new KnowledgeBaseVectorSearchConfiguration
                            {
                                NumberOfResults = numberOfResults,
                                OverrideSearchType = SearchType.HYBRID
                            }
                        }
                    }
                }
            };

            var response = await _client.RetrieveAndGenerateAsync(request, cancellationToken);

            var citations = new List<RetrievedDocument>();
            if (response.Citations != null)
            {
                foreach (var citation in response.Citations)
                {
                    if (citation.RetrievedReferences != null)
                    {
                        foreach (var reference in citation.RetrievedReferences)
                        {
                            citations.Add(new RetrievedDocument
                            {
                                Content = reference.Content?.Text ?? string.Empty,
                                SourceUri = reference.Location?.ConfluenceLocation?.Url
                                           ?? reference.Location?.S3Location?.Uri
                                           ?? reference.Location?.WebLocation?.Url,
                                Metadata = ExtractMetadataFromReference(reference)
                            });
                        }
                    }
                }
            }

            _logger?.LogInformation(
                "RAG 查询完成，生成响应长度: {Length}, 引用数: {CitationCount}",
                response.Output?.Text?.Length ?? 0, citations.Count);

            return KnowledgeBaseRagResult.Success(
                response.Output?.Text ?? string.Empty,
                citations);
        }
        catch (ResourceNotFoundException ex)
        {
            _logger?.LogWarning(ex, "Knowledge Base 不存在");
            return KnowledgeBaseRagResult.Failure($"Knowledge Base 不存在: {ex.Message}");
        }
        catch (ValidationException ex)
        {
            _logger?.LogWarning(ex, "请求验证失败");
            return KnowledgeBaseRagResult.Failure($"请求验证失败: {ex.Message}");
        }
        catch (AmazonBedrockAgentRuntimeException ex)
        {
            _logger?.LogError(ex, "RAG 查询失败: {ErrorCode}", ex.ErrorCode);
            return KnowledgeBaseRagResult.Failure($"RAG 查询失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RAG 查询发生未知错误");
            return KnowledgeBaseRagResult.Failure($"查询失败: {ex.Message}");
        }
    }

    private static string? ExtractTitle(KnowledgeBaseRetrievalResult result)
    {
        if (result.Metadata != null && result.Metadata.TryGetValue("title", out var titleDoc))
        {
            return titleDoc.ToString();
        }

        if (result.Location?.ConfluenceLocation != null)
        {
            return result.Location.ConfluenceLocation.Url;
        }

        return null;
    }

    private static Dictionary<string, string> ExtractMetadata(KnowledgeBaseRetrievalResult result)
    {
        var metadata = new Dictionary<string, string>();

        if (result.Metadata != null)
        {
            foreach (var kvp in result.Metadata)
            {
                try
                {
                    metadata[kvp.Key] = kvp.Value.ToString();
                }
                catch
                {
                    metadata[kvp.Key] = string.Empty;
                }
            }
        }

        if (result.Location?.Type != null)
        {
            metadata["locationType"] = result.Location.Type.Value;
        }

        return metadata;
    }

    private static Dictionary<string, string> ExtractMetadataFromReference(RetrievedReference reference)
    {
        var metadata = new Dictionary<string, string>();

        if (reference.Metadata != null)
        {
            foreach (var kvp in reference.Metadata)
            {
                try
                {
                    metadata[kvp.Key] = kvp.Value.ToString();
                }
                catch
                {
                    metadata[kvp.Key] = string.Empty;
                }
            }
        }

        return metadata;
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Knowledge Base 服务配置选项
/// </summary>
public class KnowledgeBaseServiceOptions
{
    /// <summary>
    /// AWS 区域
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// 自定义服务 URL（用于本地测试）
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Knowledge Base ID
    /// </summary>
    public string? KnowledgeBaseId { get; set; }

    /// <summary>
    /// Foundation Model ARN（用于 RAG）
    /// 例如: arn:aws:bedrock:ap-northeast-1::foundation-model/anthropic.claude-3-sonnet-20240229-v1:0
    /// </summary>
    public string? FoundationModelArn { get; set; }
}
