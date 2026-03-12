using Microsoft.AspNetCore.Mvc;
using SreAgent.Application.Services;

namespace SreAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalsController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpGet("/api/approvals/pending")]
    public async Task<IActionResult> GetPending([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 100)
            return BadRequest(new { error = "limit must be between 1 and 100" });

        var (items, total) = await _approvalService.GetPendingAsync(limit, ct);
        return Ok(new ApprovalPendingListResponse
        {
            Items = items.Select(item => new ApprovalPendingItem
            {
                SessionId = item.Id,
                AlertName = item.AlertName,
                ServiceName = item.ServiceName,
                Status = item.Status,
                UpdatedAt = item.UpdatedAt
            }).ToList(),
            Total = total
        });
    }

    [HttpPost("/api/approvals/{sessionId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid sessionId,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ApproverId))
            return BadRequest(new { error = "approverId cannot be empty" });

        try
        {
            var result = await _approvalService.ApproveAsync(sessionId, request.ApproverId.Trim(), request.Comment, ct);
            return Ok(new ApprovalDecisionResponse
            {
                SessionId = result.SessionId,
                Status = result.Status,
                Message = result.Message
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("/api/approvals/{sessionId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid sessionId,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ApproverId))
            return BadRequest(new { error = "approverId cannot be empty" });

        try
        {
            var result = await _approvalService.RejectAsync(sessionId, request.ApproverId.Trim(), request.Comment, ct);
            return Ok(new ApprovalDecisionResponse
            {
                SessionId = result.SessionId,
                Status = result.Status,
                Message = result.Message
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("/api/approvals/history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (limit < 1 || limit > 200)
            return BadRequest(new { error = "limit must be between 1 and 200" });

        var (items, total) = await _approvalService.GetHistoryAsync(limit, ct);
        return Ok(new ApprovalHistoryResponse
        {
            Items = items.Select(item => new ApprovalHistoryItem
            {
                Id = item.Id,
                SessionId = item.SessionId,
                Action = item.Type,
                Reason = item.Reason,
                IntervenedBy = item.IntervenedBy,
                IntervenedAt = item.IntervenedAt
            }).ToList(),
            Total = total
        });
    }
}

public class ApprovalDecisionRequest
{
    public string ApproverId { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

public class ApprovalPendingListResponse
{
    public List<ApprovalPendingItem> Items { get; set; } = [];
    public int Total { get; set; }
}

public class ApprovalPendingItem
{
    public Guid SessionId { get; set; }
    public string? AlertName { get; set; }
    public string? ServiceName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class ApprovalDecisionResponse
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ApprovalHistoryResponse
{
    public List<ApprovalHistoryItem> Items { get; set; } = [];
    public int Total { get; set; }
}

public class ApprovalHistoryItem
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? IntervenedBy { get; set; }
    public DateTime IntervenedAt { get; set; }
}
