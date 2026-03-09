using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;
using SreAgent.Repository;

namespace SreAgent.Application.Tools.DiagnosticData;

public class QueryDiagnosticDataTool : ToolBase<QueryDiagnosticDataParams>
{
    private readonly AppDbContext _dbContext;
    private const int MaxLimit = 200;

    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "diagnostic_data"
    };

    private static readonly Regex DmlPattern = new(
        @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|CREATE|GRANT|REVOKE|EXEC|EXECUTE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public QueryDiagnosticDataTool(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override string Name => "query_diagnostic_data";
    public override string Summary => "Execute a restricted SQL SELECT query against stored diagnostic data";
    public override string Description => """
        Execute a SQL SELECT query against the diagnostic_data table.
        Restrictions:
        - Only SELECT statements are allowed (no INSERT, UPDATE, DELETE, DROP, etc.)
        - Only the diagnostic_data table can be queried
        - A WHERE session_id filter is automatically injected
        - Results are limited to 200 rows max
        The session_id condition is added automatically - do NOT include it in your query.
        Example: SELECT severity, count(*) as cnt FROM diagnostic_data GROUP BY severity
        """;
    public override string Category => "Diagnostics";

    protected override async Task<ToolResult> ExecuteAsync(
        QueryDiagnosticDataParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sql = parameters.Sql?.Trim();
        if (string.IsNullOrWhiteSpace(sql))
            return ToolResult.Failure("SQL query cannot be empty.", "INVALID_SQL");

        var validationError = ValidateSql(sql);
        if (validationError != null)
            return ToolResult.Failure(validationError, "SQL_REJECTED");

        var safeSql = InjectSessionFilter(sql, context.SessionId);
        safeSql = EnforceLimit(safeSql);

        try
        {
            using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = safeSql;
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var sb = new StringBuilder();

            // Header
            var fieldCount = reader.FieldCount;
            var columnNames = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++)
            {
                columnNames[i] = reader.GetName(i);
            }
            sb.AppendLine(string.Join(" | ", columnNames));
            sb.AppendLine(new string('-', columnNames.Sum(c => c.Length) + (fieldCount - 1) * 3));

            var rowCount = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new string[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    values[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "";
                }
                sb.AppendLine(string.Join(" | ", values));
                rowCount++;
            }

            sb.Insert(0, $"Query returned {rowCount} rows:\n\n");
            return ToolResult.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"SQL execution failed: {ex.Message}", "SQL_ERROR", isRetryable: true);
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    internal static string? ValidateSql(string sql)
    {
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return "Only SELECT statements are allowed.";

        if (DmlPattern.IsMatch(sql))
            return "DML/DDL statements (INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, etc.) are not allowed.";

        // Check table references
        var fromPattern = new Regex(@"\bFROM\s+(\w+)", RegexOptions.IgnoreCase);
        var joinPattern = new Regex(@"\bJOIN\s+(\w+)", RegexOptions.IgnoreCase);

        foreach (Match match in fromPattern.Matches(sql))
        {
            var table = match.Groups[1].Value;
            if (!AllowedTables.Contains(table))
                return $"Table '{table}' is not allowed. Only these tables can be queried: {string.Join(", ", AllowedTables)}.";
        }
        foreach (Match match in joinPattern.Matches(sql))
        {
            var table = match.Groups[1].Value;
            if (!AllowedTables.Contains(table))
                return $"Table '{table}' is not allowed. Only these tables can be queried: {string.Join(", ", AllowedTables)}.";
        }

        return null;
    }

    internal static string InjectSessionFilter(string sql, Guid sessionId)
    {
        // If there's a WHERE clause, append AND; otherwise add WHERE
        var sessionFilter = $"session_id = '{sessionId}'";

        if (Regex.IsMatch(sql, @"\bWHERE\b", RegexOptions.IgnoreCase))
        {
            var whereIndex = Regex.Match(sql, @"\bWHERE\b", RegexOptions.IgnoreCase).Index;
            var afterWhere = whereIndex + 5; // "WHERE" length
            return sql.Insert(afterWhere, $" {sessionFilter} AND");
        }

        // Find position to insert WHERE (before GROUP BY, ORDER BY, LIMIT, or end)
        var insertPattern = new Regex(@"\b(GROUP\s+BY|ORDER\s+BY|LIMIT|HAVING|$)", RegexOptions.IgnoreCase);
        var insertMatch = insertPattern.Match(sql);
        if (insertMatch.Success && insertMatch.Index > 0)
        {
            return sql.Insert(insertMatch.Index, $" WHERE {sessionFilter} ");
        }

        return sql + $" WHERE {sessionFilter}";
    }

    internal static string EnforceLimit(string sql)
    {
        if (Regex.IsMatch(sql, @"\bLIMIT\s+\d+", RegexOptions.IgnoreCase))
        {
            return Regex.Replace(sql, @"\bLIMIT\s+(\d+)", match =>
            {
                var currentLimit = int.Parse(match.Groups[1].Value);
                return $"LIMIT {Math.Min(currentLimit, MaxLimit)}";
            }, RegexOptions.IgnoreCase);
        }

        return sql + $" LIMIT {MaxLimit}";
    }
}

public class QueryDiagnosticDataParams
{
    [Required]
    [Description("SQL SELECT query to execute against diagnostic_data table. session_id filter is auto-injected. Example: SELECT severity, count(*) as cnt FROM diagnostic_data GROUP BY severity")]
    public string Sql { get; set; } = string.Empty;
}
