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

    [HttpGet("/api/approvals/rules")]
    public async Task<IActionResult> GetRules(CancellationToken ct = default)
    {
        var rules = await _approvalService.GetRulesAsync(ct);
        return Ok(new ApprovalRulesListResponse
        {
            Items = rules.Select(r => new ApprovalRuleItem
            {
                Id = r.Id,
                ToolName = r.ToolName,
                RuleType = r.RuleType,
                CreatedBy = r.CreatedBy,
                CreatedAt = r.CreatedAt
            }).ToList(),
            Total = rules.Count
        });
    }

    [HttpPost("/api/approvals/rules")]
    public async Task<IActionResult> CreateRule(
        [FromBody] CreateApprovalRuleRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ToolName))
            return BadRequest(new { error = "toolName cannot be empty" });

        try
        {
            var rule = await _approvalService.CreateRuleAsync(
                request.ToolName, request.RuleType, request.CreatedBy, ct);
            return Created($"/api/approvals/rules/{rule.Id}", new ApprovalRuleItem
            {
                Id = rule.Id,
                ToolName = rule.ToolName,
                RuleType = rule.RuleType,
                CreatedBy = rule.CreatedBy,
                CreatedAt = rule.CreatedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("/api/approvals/rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        var deleted = await _approvalService.DeleteRuleAsync(id, ct);
        if (!deleted) return NotFound();
        return NoContent();
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

public class ApprovalRulesListResponse
{
    public List<ApprovalRuleItem> Items { get; set; } = [];
    public int Total { get; set; }
}

public class ApprovalRuleItem
{
    public Guid Id { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateApprovalRuleRequest
{
    public string ToolName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
}
