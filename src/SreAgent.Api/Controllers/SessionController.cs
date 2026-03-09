using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SreAgent.Application.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Repository.Repositories;

namespace SreAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ICheckpointService _checkpointService;
    private readonly IInterventionService _interventionService;
    private readonly ISessionRecoveryService _recoveryService;
    private readonly IAuditService _auditService;
    private readonly IAgent _agent;
    private readonly ILogger<SessionController> _logger;

    public SessionController(
        ISessionRepository sessionRepository,
        ICheckpointService checkpointService,
        IInterventionService interventionService,
        ISessionRecoveryService recoveryService,
        IAuditService auditService,
        IAgent agent,
        ILogger<SessionController> logger)
    {
        _sessionRepository = sessionRepository;
        _checkpointService = checkpointService;
        _interventionService = interventionService;
        _recoveryService = recoveryService;
        _auditService = auditService;
        _agent = agent;
        _logger = logger;
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct);
        if (session == null)
            return NotFound();

        return Ok(new
        {
            session.Id,
            session.Status,
            session.AlertId,
            session.AlertName,
            session.CurrentAgentId,
            session.CurrentStep,
            session.DiagnosisSummary,
            session.Confidence,
            session.CreatedAt,
            session.StartedAt,
            session.CompletedAt,
            session.UpdatedAt
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
