using FluentAssertions;
using SreAgent.Application.Tools.DiagnosticData;
using Xunit;

namespace SreAgent.Application.Tests.Tools.DiagnosticData;

public class QueryDiagnosticDataToolTests
{
    #region SQL Validation

    [Theory]
    [InlineData("SELECT * FROM diagnostic_data")]
    [InlineData("SELECT severity, count(*) FROM diagnostic_data GROUP BY severity")]
    [InlineData("SELECT content FROM diagnostic_data WHERE severity = 'ERROR' LIMIT 50")]
    public void ValidateSql_WithValidSelectQueries_ShouldReturnNull(string sql)
    {
        var result = QueryDiagnosticDataTool.ValidateSql(sql);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("INSERT INTO diagnostic_data (content) VALUES ('test')")]
    [InlineData("UPDATE diagnostic_data SET content = 'hacked'")]
    [InlineData("DELETE FROM diagnostic_data")]
    [InlineData("DROP TABLE diagnostic_data")]
    [InlineData("ALTER TABLE diagnostic_data ADD COLUMN hack TEXT")]
    [InlineData("TRUNCATE diagnostic_data")]
    public void ValidateSql_WithDmlStatements_ShouldReject(string sql)
    {
        var result = QueryDiagnosticDataTool.ValidateSql(sql);
        result.Should().NotBeNull("DML/DDL statements must be rejected");
    }

    [Fact]
    public void ValidateSql_WithNonSelectStatement_ShouldReject()
    {
        var result = QueryDiagnosticDataTool.ValidateSql("EXPLAIN SELECT * FROM diagnostic_data");
        result.Should().NotBeNull();
        result.Should().Contain("Only SELECT");
    }

    [Theory]
    [InlineData("SELECT * FROM sessions")]
    [InlineData("SELECT * FROM messages")]
    [InlineData("SELECT * FROM audit_logs")]
    public void ValidateSql_WithDisallowedTables_ShouldReject(string sql)
    {
        var result = QueryDiagnosticDataTool.ValidateSql(sql);
        result.Should().NotBeNull();
        result.Should().Contain("not allowed");
    }

    [Fact]
    public void ValidateSql_WithJoinToDisallowedTable_ShouldReject()
    {
        var sql = "SELECT d.* FROM diagnostic_data d JOIN sessions s ON d.session_id = s.id";
        var result = QueryDiagnosticDataTool.ValidateSql(sql);
        result.Should().NotBeNull();
        result.Should().Contain("sessions");
    }

    #endregion

    #region Session Filter Injection

    [Fact]
    public void InjectSessionFilter_WithNoWhereClause_ShouldAddWhere()
    {
        var sessionId = Guid.NewGuid();
        var sql = "SELECT * FROM diagnostic_data";
        var result = QueryDiagnosticDataTool.InjectSessionFilter(sql, sessionId);
        result.Should().Contain($"session_id = '{sessionId}'");
        result.Should().Contain("WHERE");
    }

    [Fact]
    public void InjectSessionFilter_WithExistingWhereClause_ShouldAddAnd()
    {
        var sessionId = Guid.NewGuid();
        var sql = "SELECT * FROM diagnostic_data WHERE severity = 'ERROR'";
        var result = QueryDiagnosticDataTool.InjectSessionFilter(sql, sessionId);
        result.Should().Contain($"session_id = '{sessionId}'");
        result.Should().Contain("AND");
    }

    [Fact]
    public void InjectSessionFilter_WithGroupBy_ShouldInsertBeforeGroupBy()
    {
        var sessionId = Guid.NewGuid();
        var sql = "SELECT severity, count(*) FROM diagnostic_data GROUP BY severity";
        var result = QueryDiagnosticDataTool.InjectSessionFilter(sql, sessionId);
        result.Should().Contain($"session_id = '{sessionId}'");
        var whereIndex = result.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        var groupByIndex = result.IndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
        whereIndex.Should().BeLessThan(groupByIndex);
    }

    #endregion

    #region Limit Enforcement

    [Fact]
    public void EnforceLimit_WithNoLimit_ShouldAppendLimit200()
    {
        var sql = "SELECT * FROM diagnostic_data";
        var result = QueryDiagnosticDataTool.EnforceLimit(sql);
        result.Should().EndWith("LIMIT 200");
    }

    [Fact]
    public void EnforceLimit_WithSmallLimit_ShouldKeepOriginal()
    {
        var sql = "SELECT * FROM diagnostic_data LIMIT 50";
        var result = QueryDiagnosticDataTool.EnforceLimit(sql);
        result.Should().Contain("LIMIT 50");
    }

    [Fact]
    public void EnforceLimit_WithLargeLimit_ShouldCapAt200()
    {
        var sql = "SELECT * FROM diagnostic_data LIMIT 9999";
        var result = QueryDiagnosticDataTool.EnforceLimit(sql);
        result.Should().Contain("LIMIT 200");
    }

    #endregion
}
