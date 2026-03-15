using Microsoft.AspNetCore.Mvc;
using SreAgent.Application.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Repository.Repositories;

namespace SreAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToolsController : ControllerBase
{
    private const string RuleAlwaysAllow = "always-allow";
    private const string RuleAlwaysDeny = "always-deny";
    private const string RuleRequireApproval = "require-approval";

    private readonly IAgent _agent;
    private readonly IApprovalService _approvalService;
    private readonly IToolInvocationRepository _toolInvocationRepository;

    public ToolsController(
        IAgent agent,
        IApprovalService approvalService,
        IToolInvocationRepository toolInvocationRepository)
    {
        _agent = agent;
        _approvalService = approvalService;
        _toolInvocationRepository = toolInvocationRepository;
    }

    [HttpGet("/api/tools")]
    public async Task<IActionResult> GetTools(CancellationToken ct = default)
    {
        var tools = _agent.Options.Tools
            .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(tool => tool.Category)
            .ThenBy(tool => tool.Name)
            .ToList();

        var toolNames = tools.Select(tool => tool.Name).ToArray();
        var stats = await _toolInvocationRepository.GetStatsByToolNamesAsync(toolNames, ct);
        var statsByTool = stats.ToDictionary(s => s.ToolName, StringComparer.OrdinalIgnoreCase);

        var rules = await _approvalService.GetRulesAsync(ct);
        var latestRuleByTool = rules
            .GroupBy(r => r.ToolName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(r => r.CreatedAt).First().RuleType,
                StringComparer.OrdinalIgnoreCase);

        var items = tools
            .Select(tool =>
            {
                statsByTool.TryGetValue(tool.Name, out var toolStats);
                latestRuleByTool.TryGetValue(tool.Name, out var ruleType);
                return ToToolRegistryItem(tool, toolStats, ruleType);
            })
            .ToList();

        return Ok(new ToolRegistryResponse
        {
            Items = items,
            Total = items.Count
        });
    }

    [HttpPut("/api/tools/{toolName}/approval-mode")]
    public async Task<IActionResult> UpdateToolApprovalMode(
        string toolName,
        [FromBody] UpdateToolApprovalModeRequest? request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return BadRequest(new { error = "toolName cannot be empty" });

        if (request is null)
            return BadRequest(new { error = "request body is required" });

        var normalizedToolName = toolName.Trim();
        var tool = _agent.Options.Tools.FirstOrDefault(t =>
            string.Equals(t.Name, normalizedToolName, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
            return NotFound(new { error = $"tool '{normalizedToolName}' not found" });

        var targetRuleType = request.AutoApprove ? RuleAlwaysAllow : RuleRequireApproval;
        var rule = await _approvalService.UpsertRuleAsync(
            tool.Name,
            targetRuleType,
            request.UpdatedBy,
            ct);

        var stats = await _toolInvocationRepository.GetStatsByToolNamesAsync([tool.Name], ct);
        var toolStats = stats.FirstOrDefault();
        var item = ToToolRegistryItem(tool, toolStats, rule.RuleType);
        return Ok(item);
    }

    private static ToolRegistryItem ToToolRegistryItem(ITool tool, ToolInvocationStats? stats, string? ruleType)
    {
        var invocations = stats?.Invocations ?? 0;
        var successCount = stats?.SuccessCount ?? 0;
        var successRate = invocations == 0 ? 0 : Math.Round((double)successCount * 100 / invocations, 1);
        var (approvalMode, autoApprove) = MapApprovalMode(ruleType);

        return new ToolRegistryItem
        {
            Name = tool.Name,
            Summary = tool.Summary,
            Category = tool.Category,
            ApprovalMode = approvalMode,
            AutoApprove = autoApprove,
            Invocations = invocations,
            SuccessRate = successRate,
            AvgDurationMs = stats?.AvgDurationMs ?? 0
        };
    }

    private static (string ApprovalMode, bool AutoApprove) MapApprovalMode(string? ruleType)
    {
        return ruleType switch
        {
            RuleRequireApproval => (RuleRequireApproval, false),
            RuleAlwaysDeny => (RuleAlwaysDeny, false),
            _ => ("auto-approve", true)
        };
    }
}

public class ToolRegistryResponse
{
    public List<ToolRegistryItem> Items { get; set; } = [];
    public int Total { get; set; }
}

public class ToolRegistryItem
{
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ApprovalMode { get; set; } = "auto-approve";
    public bool AutoApprove { get; set; } = true;
    public int Invocations { get; set; }
    public double SuccessRate { get; set; }
    public long AvgDurationMs { get; set; }
}

public class UpdateToolApprovalModeRequest
{
    public bool AutoApprove { get; set; }
    public string? UpdatedBy { get; set; }
}
