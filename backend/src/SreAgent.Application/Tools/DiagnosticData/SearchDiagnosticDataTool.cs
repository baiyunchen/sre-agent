using System.ComponentModel;
using System.Text;
using SreAgent.Application.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Application.Tools.DiagnosticData;

public class SearchDiagnosticDataTool : ToolBase<SearchDiagnosticDataParams>
{
    private readonly IDiagnosticDataService _service;

    public SearchDiagnosticDataTool(IDiagnosticDataService service)
    {
        _service = service;
    }

    public override string Name => "search_diagnostic_data";
    public override string Summary => "Search stored diagnostic data (logs/metrics) by keyword, severity, source, and time range";
    public override string Description => """
        Search previously stored diagnostic data (logs, metrics) in the database.
        Use this tool to find specific log entries by keyword, severity level, source type, or time range.
        Data is stored when CloudWatch or other monitoring tools return large result sets.
        Results are scoped to the current session.
        """;
    public override string Category => "Diagnostics";

    protected override async Task<ToolResult> ExecuteAsync(
        SearchDiagnosticDataParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        DateTime? startTime = null, endTime = null;
        if (!string.IsNullOrWhiteSpace(parameters.StartTime) && DateTime.TryParse(parameters.StartTime, out var st))
            startTime = st.ToUniversalTime();
        if (!string.IsNullOrWhiteSpace(parameters.EndTime) && DateTime.TryParse(parameters.EndTime, out var et))
            endTime = et.ToUniversalTime();

        var result = await _service.SearchAsync(new DiagnosticSearchRequest
        {
            SessionId = context.SessionId,
            Keyword = parameters.Keyword,
            Severity = parameters.Severity?.ToUpperInvariant(),
            SourceType = parameters.SourceType,
            StartTime = startTime,
            EndTime = endTime,
            Limit = parameters.Limit
        }, cancellationToken);

        if (result.TotalMatches == 0)
            return ToolResult.Success("No diagnostic data found matching the criteria.");

        var sb = new StringBuilder();
        sb.AppendLine($"Found {result.TotalMatches} matching records (showing up to {parameters.Limit}):");
        sb.AppendLine();

        foreach (var item in result.Results)
        {
            var ts = item.LogTimestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "N/A";
            var sev = item.Severity ?? "N/A";
            sb.AppendLine($"[{ts}] [{sev}] [{item.SourceType}] {item.Content}");
        }

        return ToolResult.Success(sb.ToString());
    }
}

public class SearchDiagnosticDataParams
{
    [Description("Keyword to search for in log content (case-sensitive substring match)")]
    public string? Keyword { get; set; }

    [Description("Filter by severity level: ERROR, WARN, INFO, DEBUG")]
    public string? Severity { get; set; }

    [Description("Filter by data source type: cloudwatch_logs, prometheus_metrics, etc.")]
    public string? SourceType { get; set; }

    [Description("Start time filter (ISO 8601 format)")]
    public string? StartTime { get; set; }

    [Description("End time filter (ISO 8601 format)")]
    public string? EndTime { get; set; }

    [Description("Maximum number of results to return (default 50, max 200)")]
    public int Limit { get; set; } = 50;
}
