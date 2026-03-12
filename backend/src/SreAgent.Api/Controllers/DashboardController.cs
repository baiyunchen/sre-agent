using Microsoft.AspNetCore.Mvc;
using SreAgent.Repository.Repositories;

namespace SreAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
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
        var startOfDayUtc = DateTime.UtcNow.Date;
        var stats = await _sessionRepository.GetDashboardStatsAsync(startOfDayUtc, ct);
        return Ok(new DashboardStatsResponse
        {
            TotalSessionsToday = stats.TotalSessionsToday,
            AutoResolutionRate = stats.AutoResolutionRate,
            AvgProcessingTimeSeconds = stats.AvgProcessingTimeSeconds,
            PendingApprovals = stats.PendingApprovals
        });
    }

    [HttpGet("/api/dashboard/active-sessions")]
    public async Task<IActionResult> GetActiveSessions([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 50)
            return BadRequest(new { error = "limit must be between 1 and 50" });

        var (items, total) = await _sessionRepository.GetActiveSessionsAsync(limit, ct);
        return Ok(new DashboardActiveSessionsResponse
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
        });
    }

    [HttpGet("/api/dashboard/activities")]
    public async Task<IActionResult> GetActivities([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 100)
            return BadRequest(new { error = "limit must be between 1 and 100" });

        var (items, total) = await _auditLogRepository.GetRecentAsync(limit, ct);
        return Ok(new DashboardActivitiesResponse
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
        });
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
