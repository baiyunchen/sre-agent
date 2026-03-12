using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SreAgent.Repository.Repositories;

namespace SreAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private const int DefaultStreamActiveSessionLimit = 8;
    private const int DefaultStreamActivityLimit = 12;
    private static readonly TimeSpan StreamPushInterval = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions StreamSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISessionRepository _sessionRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public DashboardController(
        ISessionRepository sessionRepository,
        IAuditLogRepository auditLogRepository)
    {
        _sessionRepository = sessionRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet("/api/dashboard/stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        return Ok(await BuildStatsResponseAsync(ct));
    }

    [HttpGet("/api/dashboard/active-sessions")]
    public async Task<IActionResult> GetActiveSessions([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 50)
            return BadRequest(new { error = "limit must be between 1 and 50" });

        return Ok(await BuildActiveSessionsResponseAsync(limit, ct));
    }

    [HttpGet("/api/dashboard/activities")]
    public async Task<IActionResult> GetActivities([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 100)
            return BadRequest(new { error = "limit must be between 1 and 100" });

        return Ok(await BuildActivitiesResponseAsync(limit, ct));
    }

    [HttpGet("/api/events/stream")]
    public async Task GetEventsStream(CancellationToken ct)
    {
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.ContentType = "text/event-stream";

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var snapshot = await BuildSnapshotEventAsync(ct);
                await WriteSseEventAsync("dashboard.snapshot", snapshot, ct);
                await Task.Delay(StreamPushInterval, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected; swallow cancellation to end stream gracefully.
        }
    }

    private async Task<DashboardStatsResponse> BuildStatsResponseAsync(CancellationToken ct)
    {
        var startOfDayUtc = DateTime.UtcNow.Date;
        var stats = await _sessionRepository.GetDashboardStatsAsync(startOfDayUtc, ct);
        return new DashboardStatsResponse
        {
            TotalSessionsToday = stats.TotalSessionsToday,
            AutoResolutionRate = stats.AutoResolutionRate,
            AvgProcessingTimeSeconds = stats.AvgProcessingTimeSeconds,
            PendingApprovals = stats.PendingApprovals
        };
    }

    private async Task<DashboardActiveSessionsResponse> BuildActiveSessionsResponseAsync(int limit, CancellationToken ct)
    {
        var (items, total) = await _sessionRepository.GetActiveSessionsAsync(limit, ct);
        return new DashboardActiveSessionsResponse
        {
            Items = items.Select(session => new DashboardActiveSessionSummary
            {
                Id = session.Id,
                AlertName = session.AlertName,
                ServiceName = session.ServiceName,
                Status = session.Status,
                CurrentStep = session.CurrentStep,
                StartedAt = session.StartedAt,
                UpdatedAt = session.UpdatedAt
            }).ToList(),
            Total = total
        };
    }

    private async Task<DashboardActivitiesResponse> BuildActivitiesResponseAsync(int limit, CancellationToken ct)
    {
        var (items, total) = await _auditLogRepository.GetRecentAsync(limit, ct);
        return new DashboardActivitiesResponse
        {
            Items = items.Select(log => new DashboardActivityItem
            {
                Id = log.Id,
                SessionId = log.SessionId,
                EventType = log.EventType,
                Description = log.EventDescription,
                Actor = log.Actor,
                OccurredAt = log.OccurredAt
            }).ToList(),
            Total = total
        };
    }

    private async Task<DashboardSnapshotEvent> BuildSnapshotEventAsync(CancellationToken ct)
    {
        var stats = await BuildStatsResponseAsync(ct);
        var activeSessions = await BuildActiveSessionsResponseAsync(DefaultStreamActiveSessionLimit, ct);
        var activities = await BuildActivitiesResponseAsync(DefaultStreamActivityLimit, ct);

        return new DashboardSnapshotEvent
        {
            EventType = "dashboard.snapshot",
            GeneratedAt = DateTime.UtcNow,
            Stats = stats,
            ActiveSessions = activeSessions,
            Activities = activities
        };
    }

    private async Task WriteSseEventAsync(string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, StreamSerializerOptions);
        await Response.WriteAsync($"event: {eventName}\n", ct);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}

public class DashboardStatsResponse
{
    public int TotalSessionsToday { get; set; }
    public double AutoResolutionRate { get; set; }
    public int AvgProcessingTimeSeconds { get; set; }
    public int PendingApprovals { get; set; }
}

public class DashboardActiveSessionsResponse
{
    public List<DashboardActiveSessionSummary> Items { get; set; } = [];
    public int Total { get; set; }
}

public class DashboardActiveSessionSummary
{
    public Guid Id { get; set; }
    public string? AlertName { get; set; }
    public string? ServiceName { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DashboardActivitiesResponse
{
    public List<DashboardActivityItem> Items { get; set; } = [];
    public int Total { get; set; }
}

public class DashboardActivityItem
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Actor { get; set; }
    public DateTime OccurredAt { get; set; }
}

public class DashboardSnapshotEvent
{
    public string EventType { get; set; } = "dashboard.snapshot";
    public DateTime GeneratedAt { get; set; }
    public DashboardStatsResponse Stats { get; set; } = new();
    public DashboardActiveSessionsResponse ActiveSessions { get; set; } = new();
    public DashboardActivitiesResponse Activities { get; set; } = new();
}
