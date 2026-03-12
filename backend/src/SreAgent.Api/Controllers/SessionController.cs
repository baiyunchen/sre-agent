using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SreAgent.Api.Models;
using SreAgent.Application.Services;
using SreAgent.Application.Tools.Todo.Models;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;
using SreAgent.Repository.Repositories;

namespace SreAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private static readonly HashSet<string> AllowedSortFields =
        new(["createdAt", "updatedAt", "status"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedSortOrders =
        new(["asc", "desc"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> MessageAllowedSessionStatuses =
        new(["Running"], StringComparer.OrdinalIgnoreCase);

    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IAgentRunRepository _agentRunRepository;
    private readonly IDiagnosticDataRepository _diagnosticDataRepository;
    private readonly ITodoService _todoService;
    private readonly IContextStore _contextStore;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly ContextManagerOptions _contextOptions;
    private readonly IExecutionTracker _executionTracker;
    private readonly ICheckpointService _checkpointService;
    private readonly IInterventionService _interventionService;
    private readonly ISessionRecoveryService _recoveryService;
    private readonly IAuditService _auditService;
    private readonly IAgent _agent;
    private readonly ILogger<SessionController> _logger;

    public SessionController(
        ISessionRepository sessionRepository,
        IMessageRepository messageRepository,
        IAgentRunRepository agentRunRepository,
        IDiagnosticDataRepository diagnosticDataRepository,
        ITodoService todoService,
        IContextStore contextStore,
        ITokenEstimator tokenEstimator,
        ContextManagerOptions contextOptions,
        IExecutionTracker executionTracker,
        ICheckpointService checkpointService,
        IInterventionService interventionService,
        ISessionRecoveryService recoveryService,
        IAuditService auditService,
        IAgent agent,
        ILogger<SessionController> logger)
    {
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _agentRunRepository = agentRunRepository;
        _diagnosticDataRepository = diagnosticDataRepository;
        _todoService = todoService;
        _contextStore = contextStore;
        _tokenEstimator = tokenEstimator;
        _contextOptions = contextOptions;
        _executionTracker = executionTracker;
        _checkpointService = checkpointService;
        _interventionService = interventionService;
        _recoveryService = recoveryService;
        _auditService = auditService;
        _agent = agent;
        _logger = logger;
    }

    [HttpGet("/api/sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] GetSessionsRequest request, CancellationToken ct)
    {
        request.Sort = string.IsNullOrWhiteSpace(request.Sort) ? "createdAt" : request.Sort.Trim();
        request.SortOrder = string.IsNullOrWhiteSpace(request.SortOrder) ? "desc" : request.SortOrder.Trim();

        if (request.Page < 1)
            return BadRequest(new { error = "page must be greater than or equal to 1" });

        if (request.PageSize < 1 || request.PageSize > 100)
            return BadRequest(new { error = "pageSize must be between 1 and 100" });

        if (!AllowedSortFields.Contains(request.Sort))
            return BadRequest(new { error = "sort must be one of: createdAt, updatedAt, status" });

        if (!AllowedSortOrders.Contains(request.SortOrder))
            return BadRequest(new { error = "sortOrder must be one of: asc, desc" });

        var query = new SessionListQuery
        {
            Page = request.Page,
            PageSize = request.PageSize,
            Status = request.Status,
            Source = request.Source,
            Sort = request.Sort,
            SortOrder = request.SortOrder,
            Search = request.Search
        };

        var (items, total) = await _sessionRepository.ListAsync(query, ct);

        return Ok(new SessionListResponse
        {
            Items = items.Select(MapSessionSummary).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    [HttpGet("{sessionId:guid}")]
    [HttpGet("/api/sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct);
        if (session == null)
            return NotFound();

        return Ok(MapSessionDetail(session));
    }

    [HttpGet("/api/sessions/{sessionId:guid}/timeline")]
    public async Task<IActionResult> GetSessionTimeline(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct);
        if (session == null)
            return NotFound();

        var messagesTask = _messageRepository.GetBySessionAsync(sessionId, ct);
        var agentRunsTask = _agentRunRepository.GetBySessionAsync(sessionId, ct);
        await Task.WhenAll(messagesTask, agentRunsTask);

        var events = new List<TimelineEventResponse>();
        events.AddRange(messagesTask.Result.Select(MapMessageEvent));
        events.AddRange(agentRunsTask.Result.SelectMany(MapAgentRunEvents));

        return Ok(new SessionTimelineResponse
        {
            SessionId = sessionId,
            Events = events
                .OrderBy(e => e.Timestamp)
                .ThenBy(e => e.EventType, StringComparer.Ordinal)
                .ToList()
        });
    }

    [HttpGet("/api/sessions/{sessionId:guid}/diagnosis")]
    public async Task<IActionResult> GetSessionDiagnosis(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct);
        if (session == null)
            return NotFound();

        var summary = await _diagnosticDataRepository.GetSummaryAsync(sessionId, sourceType: null, ct);
        var evidence = await _diagnosticDataRepository.SearchAsync(
            sessionId,
            keyword: null,
            severity: null,
            sourceType: null,
            startTime: null,
            endTime: null,
            limit: 5,
            ct);

        var response = MapSessionDiagnosis(sessionId, session, summary, evidence);
        return Ok(response);
    }

    [HttpGet("/api/sessions/{sessionId:guid}/tool-invocations")]
    public async Task<IActionResult> GetSessionToolInvocations(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct);
        if (session == null)
            return NotFound();

        var agentRuns = await _agentRunRepository.GetBySessionAsync(sessionId, ct);
        var items = agentRuns
            .SelectMany(run => run.ToolInvocations.Select(tool => MapToolInvocation(run, tool)))
            .OrderByDescending(item => item.RequestedAt)
            .ThenBy(item => item.ToolName, StringComparer.Ordinal)
            .ToList();

        return Ok(new SessionToolInvocationsResponse
        {
            SessionId = sessionId,
            Items = items
        });
    }

    [HttpGet("/api/sessions/{sessionId:guid}/todos")]
    public async Task<IActionResult> GetSessionTodos(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct);
        if (session == null)
            return NotFound();

        var todos = await _todoService.GetAsync(sessionId);
        return Ok(new SessionTodosResponse
        {
            SessionId = sessionId,
            Items = todos.Select(MapTodo).ToList()
        });
    }

    [HttpPost("/api/sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> PostSessionMessage(
        Guid sessionId,
        [FromBody] SessionMessageRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message cannot be empty" });

        var session = await _sessionRepository.GetAsync(sessionId, ct);
        if (session == null)
            return NotFound();

        if (!MessageAllowedSessionStatuses.Contains(session.Status))
        {
            return Conflict(new
            {
                error = $"session status '{session.Status}' does not accept new messages"
            });
        }

        var snapshot = await _contextStore.GetAsync(sessionId, ct);
        if (snapshot == null)
            return Conflict(new { error = "session context is unavailable" });

        var userInput = request.Message.Trim();
        var context = DefaultContextManager.FromSnapshot(snapshot, _tokenEstimator, _contextOptions);
        context.AddUserMessage(userInput);

        var variables = new Dictionary<string, object>
        {
            [IExecutionTracker.VariableKey] = _executionTracker
        };

        await _auditService.LogAsync(
            sessionId,
            "SessionMessageSent",
            "Follow-up message posted to session",
            new { message = userInput },
            "user",
            request.UserId,
            ct);

        AgentResult result;
        try
        {
            result = await _agent.ExecuteAsync(context, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute follow-up message for session {SessionId}", sessionId);
            await _auditService.LogAsync(
                sessionId,
                "SessionMessageFailed",
                ex.Message,
                new { message = userInput },
                "system",
                null,
                ct);
            throw;
        }

        var metadata = new Dictionary<string, object>
        {
            ["current_agent_id"] = _agent.Id,
            ["current_step"] = result.IterationCount
        };

        if (!string.IsNullOrWhiteSpace(result.Output))
            metadata["diagnosis_summary"] = result.Output;

        await _contextStore.SaveAsync(result.Context.ExportSnapshot(sessionId, metadata), ct);

        await _auditService.LogAsync(
            sessionId,
            result.IsSuccess ? "SessionMessageProcessed" : "SessionMessageFailed",
            result.IsSuccess ? "Follow-up message processed" : (result.Error?.Message ?? "Follow-up message failed"),
            new { message = userInput, result.IsSuccess },
            "system",
            null,
            ct);

        return Ok(new SessionMessageResponse
        {
            SessionId = sessionId,
            Output = result.Output,
            IsSuccess = result.IsSuccess,
            Error = result.Error?.Message,
            TokenUsage = new TokenUsageInfo
            {
                PromptTokens = result.TokenUsage.PromptTokens,
                CompletionTokens = result.TokenUsage.CompletionTokens,
                TotalTokens = result.TokenUsage.TotalTokens
            }
        });
    }

    [HttpPost("{sessionId:guid}/interrupt")]
    public async Task<IActionResult> InterruptSession(
        Guid sessionId, [FromBody] InterventionRequest request, CancellationToken ct)
    {
        try
        {
            await _interventionService.InterruptSessionAsync(
                sessionId, request.Reason, request.UserId ?? "api", ct);
            return Ok(new { message = "Session interrupted" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{sessionId:guid}/cancel")]
    public async Task<IActionResult> CancelSession(
        Guid sessionId, [FromBody] InterventionRequest request, CancellationToken ct)
    {
        try
        {
            await _interventionService.CancelSessionAsync(
                sessionId, request.Reason, request.UserId ?? "api", ct);
            return Ok(new { message = "Session cancelled" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{sessionId:guid}/resume")]
    public async Task<IActionResult> ResumeSession(
        Guid sessionId, [FromBody] ResumeRequest? request, CancellationToken ct)
    {
        try
        {
            if (!await _recoveryService.CanResumeAsync(sessionId, ct))
                return BadRequest(new { error = "Session cannot be resumed" });

            var result = await _recoveryService.ResumeSessionAsync(
                sessionId, _agent, request?.ContinueInput, ct);

            return Ok(new
            {
                sessionId,
                result.Output,
                result.IsSuccess,
                Error = result.Error?.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{sessionId:guid}/interventions")]
    public async Task<IActionResult> GetInterventions(Guid sessionId, CancellationToken ct)
    {
        var interventions = await _interventionService.GetBySessionAsync(sessionId, ct);
        return Ok(interventions.Select(i => new
        {
            i.Id,
            i.Type,
            i.Reason,
            i.IntervenedBy,
            i.IntervenedAt
        }));
    }

    [HttpGet("{sessionId:guid}/audit")]
    public async Task<IActionResult> GetAuditLogs(Guid sessionId, CancellationToken ct)
    {
        var logs = await _auditService.GetBySessionAsync(sessionId, ct);
        return Ok(logs.Select(a => new
        {
            a.Id,
            a.EventType,
            a.EventDescription,
            a.Actor,
            a.ActorId,
            a.OccurredAt
        }));
    }

    private static SessionSummaryDto MapSessionSummary(SreAgent.Repository.Entities.SessionEntity session)
    {
        var now = DateTime.UtcNow;
        int? duration = session.StartedAt.HasValue
            ? (int)Math.Max(0, ((session.CompletedAt ?? now) - session.StartedAt.Value).TotalSeconds)
            : null;

        return new SessionSummaryDto
        {
            Id = session.Id,
            Status = session.Status,
            AlertName = session.AlertName,
            AlertId = session.AlertId,
            ServiceName = session.ServiceName,
            Source = session.AlertSource ?? ReadStringField(session.AlertData, "source", "alertSource"),
            Severity = session.AlertSeverity ?? ReadStringField(session.AlertData, "severity", "alertSeverity"),
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            Duration = duration,
            AgentSteps = session.CurrentStep
        };
    }

    private static SessionDetailResponse MapSessionDetail(SreAgent.Repository.Entities.SessionEntity session)
    {
        var now = DateTime.UtcNow;
        int? duration = session.StartedAt.HasValue
            ? (int)Math.Max(0, ((session.CompletedAt ?? now) - session.StartedAt.Value).TotalSeconds)
            : null;

        return new SessionDetailResponse
        {
            Id = session.Id,
            Status = session.Status,
            AlertId = session.AlertId,
            AlertName = session.AlertName,
            Source = session.AlertSource ?? ReadStringField(session.AlertData, "source", "alertSource"),
            Severity = session.AlertSeverity ?? ReadStringField(session.AlertData, "severity", "alertSeverity"),
            ServiceName = session.ServiceName,
            CurrentAgentId = session.CurrentAgentId,
            CurrentStep = session.CurrentStep,
            AgentSteps = session.CurrentStep,
            DiagnosisSummary = session.DiagnosisSummary,
            Confidence = session.Confidence,
            Duration = duration,
            CreatedAt = session.CreatedAt,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            UpdatedAt = session.UpdatedAt
        };
    }

    private static string? ReadStringField(JsonDocument? jsonDocument, params string[] propertyNames)
    {
        if (jsonDocument == null || jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propertyName in propertyNames)
        {
            if (!jsonDocument.RootElement.TryGetProperty(propertyName, out var valueElement))
                continue;

            if (valueElement.ValueKind != JsonValueKind.String)
                continue;

            return valueElement.GetString();
        }

        return null;
    }

    private static TimelineEventResponse MapMessageEvent(SreAgent.Repository.Entities.MessageEntity message)
    {
        return new TimelineEventResponse
        {
            Id = message.Id.ToString(),
            EventType = "message",
            Timestamp = message.CreatedAt,
            Title = $"Message: {message.Role}",
            Detail = ExtractMessageSummary(message.Parts),
            Status = null,
            Actor = message.AgentId ?? message.Role
        };
    }

    private static IEnumerable<TimelineEventResponse> MapAgentRunEvents(SreAgent.Repository.Entities.AgentRunEntity agentRun)
    {
        var actor = agentRun.AgentName ?? agentRun.AgentId;
        var runEvent = new TimelineEventResponse
        {
            Id = agentRun.Id.ToString(),
            EventType = "agent_run",
            Timestamp = agentRun.StartedAt,
            Title = $"Agent Run: {actor}",
            Detail = string.IsNullOrWhiteSpace(agentRun.ErrorMessage)
                ? $"Agent status: {agentRun.Status}"
                : agentRun.ErrorMessage,
            Status = agentRun.Status,
            Actor = actor
        };

        var toolEvents = agentRun.ToolInvocations.Select(tool => new TimelineEventResponse
        {
            Id = tool.Id.ToString(),
            EventType = "tool_invocation",
            Timestamp = tool.RequestedAt,
            Title = $"Tool: {tool.ToolName}",
            Detail = string.IsNullOrWhiteSpace(tool.ErrorMessage)
                ? $"Tool status: {tool.Status}"
                : tool.ErrorMessage,
            Status = tool.Status,
            Actor = actor
        });

        return [runEvent, .. toolEvents];
    }

    private static string? ExtractMessageSummary(JsonDocument? parts)
    {
        if (parts == null || parts.RootElement.ValueKind != JsonValueKind.Array || parts.RootElement.GetArrayLength() == 0)
            return null;

        var firstPart = parts.RootElement[0];
        if (firstPart.ValueKind != JsonValueKind.Object)
            return null;

        if (firstPart.TryGetProperty("text", out var textProperty)
            && textProperty.ValueKind == JsonValueKind.String)
        {
            return textProperty.GetString();
        }

        if (firstPart.TryGetProperty("content", out var contentProperty)
            && contentProperty.ValueKind == JsonValueKind.String)
        {
            return contentProperty.GetString();
        }

        return firstPart.GetRawText();
    }

    private static SessionDiagnosisResponse MapSessionDiagnosis(
        Guid sessionId,
        SreAgent.Repository.Entities.SessionEntity session,
        DiagnosticSummaryResult diagnosticSummary,
        IReadOnlyList<SreAgent.Repository.Entities.DiagnosticDataEntity> evidenceRecords)
    {
        var jsonHypothesis = ReadStringField(session.Diagnosis, "hypothesis");
        var hypothesis = string.IsNullOrWhiteSpace(jsonHypothesis)
            ? (session.DiagnosisSummary ?? "Diagnosis is not available yet.")
            : jsonHypothesis;

        var confidence = session.Confidence ?? ReadDoubleField(session.Diagnosis, "confidence");
        var evidence = new List<string>();
        evidence.AddRange(ReadStringArrayField(session.Diagnosis, "evidence"));
        evidence.AddRange(evidenceRecords.Select(MapEvidenceRecord));

        var recommendedActions = ReadStringArrayField(session.Diagnosis, "recommendedActions");
        if (recommendedActions.Count == 0)
            recommendedActions = ExtractBulletLines(session.DiagnosisSummary);

        return new SessionDiagnosisResponse
        {
            SessionId = sessionId,
            Hypothesis = hypothesis,
            Confidence = confidence,
            Evidence = evidence.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().Take(8).ToList(),
            RecommendedActions = recommendedActions,
            TotalRecords = diagnosticSummary.TotalRecords,
            SeverityBreakdown = diagnosticSummary.BySeverity,
            SourceBreakdown = diagnosticSummary.BySource,
            TimeWindowStart = diagnosticSummary.EarliestTimestamp,
            TimeWindowEnd = diagnosticSummary.LatestTimestamp
        };
    }

    private static ToolInvocationSummaryResponse MapToolInvocation(
        SreAgent.Repository.Entities.AgentRunEntity agentRun,
        SreAgent.Repository.Entities.ToolInvocationEntity tool)
    {
        return new ToolInvocationSummaryResponse
        {
            Id = tool.Id.ToString(),
            AgentRunId = agentRun.Id.ToString(),
            ToolName = tool.ToolName,
            Status = tool.Status,
            ApprovalStatus = tool.ApprovalStatus,
            ErrorMessage = tool.ErrorMessage,
            AgentId = agentRun.AgentId,
            AgentName = agentRun.AgentName,
            RequestedAt = tool.RequestedAt,
            CompletedAt = tool.CompletedAt,
            DurationMs = tool.DurationMs
        };
    }

    private static SessionTodoItemResponse MapTodo(TodoItem todo)
    {
        return new SessionTodoItemResponse
        {
            Id = todo.Id,
            Content = todo.Content,
            Status = todo.Status switch
            {
                TodoStatus.Pending => "pending",
                TodoStatus.InProgress => "in_progress",
                TodoStatus.Completed => "completed",
                TodoStatus.Cancelled => "cancelled",
                _ => "pending"
            },
            Priority = todo.Priority switch
            {
                TodoPriority.Low => "low",
                TodoPriority.Medium => "medium",
                TodoPriority.High => "high",
                _ => "medium"
            },
            CreatedAt = todo.CreatedAt,
            UpdatedAt = todo.UpdatedAt ?? todo.CreatedAt,
            CompletedAt = todo.CompletedAt
        };
    }

    private static string MapEvidenceRecord(SreAgent.Repository.Entities.DiagnosticDataEntity record)
    {
        var preview = record.Content.Length <= 120 ? record.Content : $"{record.Content[..120]}...";
        return $"{record.SourceType}: {preview}";
    }

    private static double? ReadDoubleField(JsonDocument? jsonDocument, params string[] propertyNames)
    {
        if (jsonDocument == null || jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propertyName in propertyNames)
        {
            if (!jsonDocument.RootElement.TryGetProperty(propertyName, out var valueElement))
                continue;

            if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDouble(out var value))
                return value;
        }

        return null;
    }

    private static List<string> ReadStringArrayField(JsonDocument? jsonDocument, params string[] propertyNames)
    {
        if (jsonDocument == null || jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
            return [];

        foreach (var propertyName in propertyNames)
        {
            if (!jsonDocument.RootElement.TryGetProperty(propertyName, out var valueElement))
                continue;

            if (valueElement.ValueKind != JsonValueKind.Array)
                continue;

            return valueElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();
        }

        return [];
    }

    private static List<string> ExtractBulletLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(8)
            .ToList();
    }
}

public class InterventionRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? UserId { get; set; }
}

public class ResumeRequest
{
    public string? ContinueInput { get; set; }
}

public class GetSessionsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public string? Source { get; set; }
    public string Sort { get; set; } = "createdAt";
    public string SortOrder { get; set; } = "desc";
    public string? Search { get; set; }
}

public class SessionListResponse
{
    public List<SessionSummaryDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SessionSummaryDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AlertName { get; set; }
    public string? AlertId { get; set; }
    public string? ServiceName { get; set; }
    public string? Source { get; set; }
    public string? Severity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? Duration { get; set; }
    public int? AgentSteps { get; set; }
}

public class SessionDetailResponse
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AlertId { get; set; }
    public string? AlertName { get; set; }
    public string? Source { get; set; }
    public string? Severity { get; set; }
    public string? ServiceName { get; set; }
    public string? CurrentAgentId { get; set; }
    public int CurrentStep { get; set; }
    public int AgentSteps { get; set; }
    public string? DiagnosisSummary { get; set; }
    public double? Confidence { get; set; }
    public int? Duration { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SessionTimelineResponse
{
    public Guid SessionId { get; set; }
    public List<TimelineEventResponse> Events { get; set; } = [];
}

public class TimelineEventResponse
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Status { get; set; }
    public string? Actor { get; set; }
}

public class SessionDiagnosisResponse
{
    public Guid SessionId { get; set; }
    public string Hypothesis { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public List<string> Evidence { get; set; } = [];
    public List<string> RecommendedActions { get; set; } = [];
    public int TotalRecords { get; set; }
    public Dictionary<string, int> SeverityBreakdown { get; set; } = [];
    public Dictionary<string, int> SourceBreakdown { get; set; } = [];
    public DateTime? TimeWindowStart { get; set; }
    public DateTime? TimeWindowEnd { get; set; }
}

public class SessionToolInvocationsResponse
{
    public Guid SessionId { get; set; }
    public List<ToolInvocationSummaryResponse> Items { get; set; } = [];
}

public class ToolInvocationSummaryResponse
{
    public string Id { get; set; } = string.Empty;
    public string AgentRunId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ApprovalStatus { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long DurationMs { get; set; }
}

public class SessionTodosResponse
{
    public Guid SessionId { get; set; }
    public List<SessionTodoItemResponse> Items { get; set; } = [];
}

public class SessionTodoItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SessionMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
}

public class SessionMessageResponse
{
    public Guid SessionId { get; set; }
    public string? Output { get; set; }
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }
    public TokenUsageInfo TokenUsage { get; set; } = new();
}
