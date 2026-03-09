using System.ComponentModel;
using System.Text;
using SreAgent.Application.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Application.Tools.DiagnosticData;

public class GetDiagnosticSummaryTool : ToolBase<GetDiagnosticSummaryParams>
{
    private readonly IDiagnosticDataService _service;

    public GetDiagnosticSummaryTool(IDiagnosticDataService service)
    {
        _service = service;
    }

    public override string Name => "get_diagnostic_summary";
    public override string Summary => "Get an aggregated summary of stored diagnostic data";
    public override string Description => """
        Get a quick overview of diagnostic data stored for the current session.
        Returns counts by severity level and data source, along with time range.
        Use this to understand what data is available before performing detailed searches.
        """;
    public override string Category => "Diagnostics";

    protected override async Task<ToolResult> ExecuteAsync(
        GetDiagnosticSummaryParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var summary = await _service.GetSummaryAsync(
            context.SessionId, parameters.SourceType, cancellationToken);

        if (summary.TotalRecords == 0)
            return ToolResult.Success("No diagnostic data stored for this session.");

        var sb = new StringBuilder();
        sb.AppendLine($"Diagnostic Data Summary ({summary.TotalRecords} total records):");
        sb.AppendLine();

        if (summary.BySeverity.Count > 0)
        {
            sb.AppendLine("By Severity:");
            foreach (var (sev, count) in summary.BySeverity.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"  {sev}: {count}");
            sb.AppendLine();
        }

        if (summary.BySource.Count > 0)
        {
            sb.AppendLine("By Source:");
            foreach (var (src, count) in summary.BySource.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"  {src}: {count}");
            sb.AppendLine();
        }

        if (summary.EarliestTimestamp.HasValue || summary.LatestTimestamp.HasValue)
        {
            sb.AppendLine($"Time Range: {summary.EarliestTimestamp:yyyy-MM-dd HH:mm:ss} ~ {summary.LatestTimestamp:yyyy-MM-dd HH:mm:ss}");
        }

        return ToolResult.Success(sb.ToString());
    }
}

public class GetDiagnosticSummaryParams
{
    [Description("Optional: filter summary by source type (e.g., 'cloudwatch_logs', 'prometheus_metrics')")]
    public string? SourceType { get; set; }
}
