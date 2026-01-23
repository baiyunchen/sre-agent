# 知识库与 RAG 实现

## 1. 概述

知识库是 SRE Agent 的核心组件之一，通过 RAG（检索增强生成）技术，让 Agent 能够：
- 检索相关的 Playbook
- 获取历史处理经验
- 提供准确的诊断建议

## 2. 知识库架构

### 2.1 数据源

| 数据源 | 内容类型 | 更新频率 |
|--------|----------|----------|
| Confluence | Playbook、架构文档 | 手动/定期同步 |
| 历史会话 | 成功的诊断案例 | 自动 |
| 服务元数据 | 服务配置信息 | 自动同步 |

### 2.2 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                      Knowledge Base                          │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │ Confluence  │  │  Historical │  │   Service   │          │
│  │  Playbooks  │  │   Cases     │  │  Metadata   │          │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘          │
│         │                │                │                  │
│         ▼                ▼                ▼                  │
│  ┌──────────────────────────────────────────────────┐       │
│  │              Document Processor                   │       │
│  │  - Chunking                                       │       │
│  │  - Metadata extraction                            │       │
│  │  - Embedding generation                           │       │
│  └────────────────────┬─────────────────────────────┘       │
│                       │                                      │
│                       ▼                                      │
│  ┌──────────────────────────────────────────────────┐       │
│  │           AWS Knowledge Base                      │       │
│  │  - Vector storage (OpenSearch Serverless)        │       │
│  │  - Retrieval API                                  │       │
│  └──────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────┘
```

## 3. 文档处理

### 3.1 文档模型

```csharp
public class Document
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public DocumentType Type { get; set; }
    public DocumentMetadata Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DocumentMetadata
{
    public string Source { get; set; }
    public string SourceId { get; set; }
    public string SourceUrl { get; set; }
    public IEnumerable<string> Services { get; set; }
    public IEnumerable<string> AlertTypes { get; set; }
    public IEnumerable<string> Tags { get; set; }
    public string Author { get; set; }
    public DateTime LastModified { get; set; }
}

public enum DocumentType
{
    Playbook,
    Architecture,
    Runbook,
    PostMortem,
    HistoricalCase
}
```

### 3.2 分块策略

```csharp
public interface IChunkingStrategy
{
    IEnumerable<DocumentChunk> Chunk(Document document);
}

public class DocumentChunk
{
    public string Id { get; set; }
    public string DocumentId { get; set; }
    public string Content { get; set; }
    public int ChunkIndex { get; set; }
    public ChunkMetadata Metadata { get; set; }
}

public class MarkdownChunkingStrategy : IChunkingStrategy
{
    private readonly int _maxChunkSize = 1000;
    private readonly int _overlapSize = 100;
    
    public IEnumerable<DocumentChunk> Chunk(Document document)
    {
        var chunks = new List<DocumentChunk>();
        
        // 按标题分割
        var sections = SplitByHeaders(document.Content);
        
        foreach (var section in sections)
        {
            if (EstimateTokens(section.Content) <= _maxChunkSize)
            {
                chunks.Add(CreateChunk(document, section, chunks.Count));
            }
            else
            {
                // 大节需要进一步分割
                var subChunks = SplitWithOverlap(section.Content, _maxChunkSize, _overlapSize);
                foreach (var subChunk in subChunks)
                {
                    chunks.Add(CreateChunk(document, section, chunks.Count, subChunk));
                }
            }
        }
        
        return chunks;
    }
    
    private DocumentChunk CreateChunk(Document doc, Section section, int index, string? content = null)
    {
        return new DocumentChunk
        {
            Id = $"{doc.Id}_{index}",
            DocumentId = doc.Id,
            Content = content ?? section.Content,
            ChunkIndex = index,
            Metadata = new ChunkMetadata
            {
                SectionTitle = section.Title,
                SectionLevel = section.Level,
                DocumentTitle = doc.Title,
                DocumentType = doc.Type,
                Services = doc.Metadata.Services,
                AlertTypes = doc.Metadata.AlertTypes
            }
        };
    }
}
```

### 3.3 Playbook 特定处理

```csharp
public class PlaybookProcessor
{
    public ProcessedPlaybook Process(Document document)
    {
        var playbook = new ProcessedPlaybook
        {
            Id = document.Id,
            Title = document.Title,
            AlertTypes = ExtractAlertTypes(document),
            Services = ExtractServices(document),
            DiagnosticSteps = ExtractDiagnosticSteps(document),
            PossibleCauses = ExtractPossibleCauses(document),
            Remediation = ExtractRemediation(document)
        };
        
        return playbook;
    }
    
    private IEnumerable<DiagnosticStep> ExtractDiagnosticSteps(Document doc)
    {
        // 解析诊断步骤
        var steps = new List<DiagnosticStep>();
        var pattern = @"(?:Step\s*\d+|第\s*\d+\s*步)[：:]\s*(.+?)(?=(?:Step\s*\d+|第\s*\d+\s*步)|$)";
        
        var matches = Regex.Matches(doc.Content, pattern, RegexOptions.Singleline);
        foreach (Match match in matches)
        {
            steps.Add(new DiagnosticStep
            {
                Description = match.Groups[1].Value.Trim(),
                Tools = ExtractMentionedTools(match.Groups[1].Value),
                Commands = ExtractCommands(match.Groups[1].Value)
            });
        }
        
        return steps;
    }
}
```

## 4. AWS Knowledge Base 集成

### 4.1 配置

```csharp
public class KnowledgeBaseOptions
{
    public string KnowledgeBaseId { get; set; }
    public string DataSourceId { get; set; }
    public string Region { get; set; } = "us-east-1";
    public int DefaultTopK { get; set; } = 5;
    public double MinRelevanceScore { get; set; } = 0.5;
}
```

### 4.2 知识库客户端

```csharp
public interface IKnowledgeBaseClient
{
    Task<IEnumerable<SearchResult>> SearchAsync(
        string query, 
        SearchFilter? filter = null, 
        int topK = 5,
        CancellationToken cancellationToken = default);
    
    Task SyncDataSourceAsync(CancellationToken cancellationToken = default);
}

public class AwsKnowledgeBaseClient : IKnowledgeBaseClient
{
    private readonly IAmazonBedrockAgentRuntime _client;
    private readonly KnowledgeBaseOptions _options;
    
    public async Task<IEnumerable<SearchResult>> SearchAsync(
        string query,
        SearchFilter? filter,
        int topK,
        CancellationToken cancellationToken)
    {
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
                    NumberOfResults = topK,
                    Filter = BuildFilter(filter)
                }
            }
        };
        
        var response = await _client.RetrieveAsync(request, cancellationToken);
        
        return response.RetrievalResults
            .Where(r => r.Score >= _options.MinRelevanceScore)
            .Select(r => new SearchResult
            {
                Content = r.Content.Text,
                Score = r.Score,
                Metadata = ParseMetadata(r.Metadata),
                Location = r.Location
            });
    }
    
    private RetrievalFilter? BuildFilter(SearchFilter? filter)
    {
        if (filter == null) return null;
        
        var conditions = new List<RetrievalFilter>();
        
        if (!string.IsNullOrEmpty(filter.Service))
        {
            conditions.Add(new RetrievalFilter
            {
                Equals = new FilterAttribute
                {
                    Key = "service",
                    Value = new Document(filter.Service)
                }
            });
        }
        
        if (!string.IsNullOrEmpty(filter.AlertType))
        {
            conditions.Add(new RetrievalFilter
            {
                Equals = new FilterAttribute
                {
                    Key = "alert_type",
                    Value = new Document(filter.AlertType)
                }
            });
        }
        
        if (conditions.Count == 0) return null;
        if (conditions.Count == 1) return conditions[0];
        
        return new RetrievalFilter
        {
            AndAll = conditions
        };
    }
}
```

### 4.3 搜索结果

```csharp
public class SearchResult
{
    public string Content { get; set; }
    public double Score { get; set; }
    public SearchResultMetadata Metadata { get; set; }
    public string Location { get; set; }
}

public class SearchResultMetadata
{
    public string DocumentId { get; set; }
    public string DocumentTitle { get; set; }
    public string DocumentType { get; set; }
    public string SectionTitle { get; set; }
    public string SourceUrl { get; set; }
    public IEnumerable<string> Services { get; set; }
    public IEnumerable<string> AlertTypes { get; set; }
}
```

## 5. Confluence 同步

### 5.1 Confluence 客户端

```csharp
public interface IConfluenceClient
{
    Task<IEnumerable<ConfluencePage>> GetPagesAsync(
        string spaceKey, 
        string? label = null,
        CancellationToken cancellationToken = default);
    
    Task<ConfluencePage> GetPageAsync(
        string pageId,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ConfluencePage>> GetUpdatedPagesAsync(
        string spaceKey,
        DateTime since,
        CancellationToken cancellationToken = default);
}

public class ConfluencePage
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string SpaceKey { get; set; }
    public string Content { get; set; }  // HTML content
    public IEnumerable<string> Labels { get; set; }
    public string Url { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Author { get; set; }
}
```

### 5.2 同步服务

```csharp
public class ConfluenceSyncService : BackgroundService
{
    private readonly IConfluenceClient _confluenceClient;
    private readonly IDocumentRepository _documentRepository;
    private readonly IKnowledgeBaseClient _knowledgeBaseClient;
    private readonly PlaybookProcessor _playbookProcessor;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly ILogger<ConfluenceSyncService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncPlaybooksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Confluence");
            }
            
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
    
    private async Task SyncPlaybooksAsync(CancellationToken cancellationToken)
    {
        var lastSync = await _documentRepository.GetLastSyncTimeAsync("confluence");
        
        // 获取更新的页面
        var pages = await _confluenceClient.GetUpdatedPagesAsync(
            _options.SpaceKey,
            lastSync,
            cancellationToken);
        
        foreach (var page in pages)
        {
            // 转换为文档
            var document = ConvertToDocument(page);
            
            // 处理 Playbook
            if (page.Labels.Contains("playbook"))
            {
                var playbook = _playbookProcessor.Process(document);
                document.Metadata.AlertTypes = playbook.AlertTypes;
                document.Metadata.Services = playbook.Services;
            }
            
            // 分块
            var chunks = _chunkingStrategy.Chunk(document);
            
            // 保存
            await _documentRepository.SaveAsync(document);
            await _documentRepository.SaveChunksAsync(chunks);
            
            _logger.LogInformation("Synced document: {Title}", document.Title);
        }
        
        // 触发知识库数据源同步
        await _knowledgeBaseClient.SyncDataSourceAsync(cancellationToken);
        
        await _documentRepository.UpdateLastSyncTimeAsync("confluence", DateTime.UtcNow);
    }
    
    private Document ConvertToDocument(ConfluencePage page)
    {
        return new Document
        {
            Id = $"confluence_{page.Id}",
            Title = page.Title,
            Content = HtmlToMarkdown(page.Content),
            Type = DetermineDocumentType(page.Labels),
            Metadata = new DocumentMetadata
            {
                Source = "confluence",
                SourceId = page.Id,
                SourceUrl = page.Url,
                Tags = page.Labels,
                Author = page.Author,
                LastModified = page.UpdatedAt
            },
            CreatedAt = page.CreatedAt,
            UpdatedAt = page.UpdatedAt
        };
    }
}
```

## 6. 历史案例学习

### 6.1 案例提取

```csharp
public class HistoricalCaseExtractor
{
    public async Task ExtractAndStoreAsync(Session session)
    {
        // 只提取成功的高置信度诊断
        if (session.Status != SessionStatus.Completed || session.Confidence < 0.8)
        {
            return;
        }
        
        var caseDocument = new Document
        {
            Id = $"case_{session.Id}",
            Title = $"Case: {session.AlertName} - {session.DiagnosisSummary}",
            Content = BuildCaseContent(session),
            Type = DocumentType.HistoricalCase,
            Metadata = new DocumentMetadata
            {
                Source = "historical_case",
                SourceId = session.Id.ToString(),
                Services = new[] { session.ServiceName },
                AlertTypes = new[] { session.AlertName },
                LastModified = session.CompletedAt.Value
            },
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.CompletedAt.Value
        };
        
        await _documentRepository.SaveAsync(caseDocument);
        
        // 分块并存储
        var chunks = _chunkingStrategy.Chunk(caseDocument);
        await _documentRepository.SaveChunksAsync(chunks);
    }
    
    private string BuildCaseContent(Session session)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"# 告警: {session.AlertName}");
        sb.AppendLine();
        sb.AppendLine($"## 服务: {session.ServiceName}");
        sb.AppendLine();
        sb.AppendLine("## 告警详情");
        sb.AppendLine(session.AlertData?.RootElement.ToString());
        sb.AppendLine();
        sb.AppendLine("## 诊断过程");
        
        foreach (var run in session.AgentRuns.OrderBy(r => r.StartedAt))
        {
            sb.AppendLine($"### {run.AgentName}");
            sb.AppendLine(run.Finding?.ToString());
            sb.AppendLine();
        }
        
        sb.AppendLine("## 诊断结论");
        sb.AppendLine(session.DiagnosisSummary);
        sb.AppendLine();
        sb.AppendLine($"置信度: {session.Confidence:P0}");
        
        return sb.ToString();
    }
}
```

## 7. RAG 增强

### 7.1 RAG 服务

```csharp
public interface IRagService
{
    Task<RagContext> RetrieveContextAsync(
        string query,
        Alert? alert = null,
        CancellationToken cancellationToken = default);
}

public class RagContext
{
    public IEnumerable<RetrievedDocument> Documents { get; set; }
    public string FormattedContext { get; set; }
    public int TotalTokens { get; set; }
}

public class RagService : IRagService
{
    private readonly IKnowledgeBaseClient _knowledgeBase;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly int _maxContextTokens = 4000;
    
    public async Task<RagContext> RetrieveContextAsync(
        string query,
        Alert? alert,
        CancellationToken cancellationToken)
    {
        // 构建搜索过滤器
        var filter = new SearchFilter
        {
            Service = alert?.Service,
            AlertType = alert?.Name
        };
        
        // 检索相关文档
        var results = await _knowledgeBase.SearchAsync(
            query, filter, topK: 10, cancellationToken);
        
        // 选择最相关的文档，限制 Token 数
        var selectedDocs = SelectDocuments(results, _maxContextTokens);
        
        // 格式化上下文
        var formattedContext = FormatContext(selectedDocs);
        
        return new RagContext
        {
            Documents = selectedDocs,
            FormattedContext = formattedContext,
            TotalTokens = _tokenEstimator.EstimateTokens(formattedContext)
        };
    }
    
    private IEnumerable<RetrievedDocument> SelectDocuments(
        IEnumerable<SearchResult> results, int maxTokens)
    {
        var selected = new List<RetrievedDocument>();
        var currentTokens = 0;
        
        foreach (var result in results.OrderByDescending(r => r.Score))
        {
            var tokens = _tokenEstimator.EstimateTokens(result.Content);
            
            if (currentTokens + tokens > maxTokens)
            {
                break;
            }
            
            selected.Add(new RetrievedDocument
            {
                Content = result.Content,
                Score = result.Score,
                Source = result.Metadata.SourceUrl,
                Title = result.Metadata.DocumentTitle
            });
            
            currentTokens += tokens;
        }
        
        return selected;
    }
    
    private string FormatContext(IEnumerable<RetrievedDocument> documents)
    {
        var sb = new StringBuilder();
        sb.AppendLine("以下是相关的参考文档：\n");
        
        foreach (var doc in documents)
        {
            sb.AppendLine($"### {doc.Title}");
            sb.AppendLine($"相关度: {doc.Score:P0}");
            sb.AppendLine($"来源: {doc.Source}");
            sb.AppendLine();
            sb.AppendLine(doc.Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
}
```

### 7.2 在 Agent 中使用 RAG

```csharp
public class PlaybookAgent : IAgent
{
    private readonly IRagService _ragService;
    private readonly IChatClient _chatClient;
    
    public async Task<AgentResult> ExecuteAsync(AgentContext context)
    {
        // 检索相关 Playbook
        var ragContext = await _ragService.RetrieveContextAsync(
            $"告警 {context.Alert.Name} 的处理步骤",
            context.Alert);
        
        // 构建提示
        var prompt = $"""
            你是一个 SRE 专家，请根据以下参考文档分析告警并提取处理步骤。
            
            {ragContext.FormattedContext}
            
            当前告警信息：
            - 告警名称: {context.Alert.Name}
            - 服务: {context.Alert.Service}
            - 描述: {context.Alert.Description}
            
            请输出：
            1. 匹配的 Playbook 及匹配度
            2. 建议的诊断步骤
            3. 可能的原因列表
            """;
        
        var response = await _chatClient.CompleteAsync(prompt);
        
        return new AgentResult
        {
            Status = AgentResultStatus.Success,
            Finding = ParseFinding(response),
            Confidence = CalculateConfidence(ragContext)
        };
    }
}
```

## 8. 配置

```yaml
knowledge_base:
  aws:
    knowledge_base_id: "${AWS_KB_ID}"
    data_source_id: "${AWS_KB_DATA_SOURCE_ID}"
    region: "us-east-1"
    
  retrieval:
    default_top_k: 5
    min_relevance_score: 0.5
    max_context_tokens: 4000
    
confluence:
  base_url: "https://yourcompany.atlassian.net/wiki"
  username: "${CONFLUENCE_USERNAME}"
  api_token: "${CONFLUENCE_API_TOKEN}"
  space_key: "SRE"
  labels:
    - "playbook"
    - "runbook"
  sync_interval_hours: 1
  
chunking:
  strategy: "markdown"
  max_chunk_size: 1000
  overlap_size: 100
  
historical_cases:
  enabled: true
  min_confidence: 0.8
  retention_days: 365
```
