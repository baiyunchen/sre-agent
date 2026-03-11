using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SreAgent.Repository;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using Xunit;

namespace SreAgent.Api.Tests;

public class DiagnosticDataRepositoryIntegrationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=sre_agent;Username=sre_agent;Password=sre_agent";

    [Fact]
    public async Task GetSummaryAndSearch_ShouldReturnExpectedDiagnostics()
    {
        var marker = $"diag-it-{Guid.NewGuid():N}";
        var sessionId = Guid.NewGuid();
        var diagnosticIds = new List<Guid>();

        await using var dbContext = CreateDbContext();
        if (!await dbContext.Database.CanConnectAsync())
            return;

        await dbContext.Database.MigrateAsync();

        var session = new SessionEntity
        {
            Id = sessionId,
            Status = "Completed",
            AlertId = $"alert-{marker}",
            AlertName = marker,
            ServiceName = $"{marker}-service",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Sessions.Add(session);

        var diagnostics = new[]
        {
            new DiagnosticDataEntity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                SourceType = "CloudWatchLogs",
                Severity = "ERROR",
                Content = $"{marker} timeout exception",
                LogTimestamp = DateTime.UtcNow.AddMinutes(-4),
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            },
            new DiagnosticDataEntity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                SourceType = "CloudWatchLogs",
                Severity = "ERROR",
                Content = $"{marker} retry failed",
                LogTimestamp = DateTime.UtcNow.AddMinutes(-3),
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            },
            new DiagnosticDataEntity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                SourceType = "Metrics",
                Severity = "WARN",
                Content = $"{marker} cpu burst",
                LogTimestamp = DateTime.UtcNow.AddMinutes(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            }
        };

        diagnosticIds.AddRange(diagnostics.Select(d => d.Id));
        dbContext.DiagnosticData.AddRange(diagnostics);
        await dbContext.SaveChangesAsync();

        try
        {
            var repository = new DiagnosticDataRepository(dbContext);

            var summary = await repository.GetSummaryAsync(sessionId, sourceType: null);
            summary.TotalRecords.Should().Be(3);
            summary.BySeverity.Should().ContainKey("ERROR").WhoseValue.Should().Be(2);
            summary.BySource.Should().ContainKey("CloudWatchLogs").WhoseValue.Should().Be(2);

            var records = await repository.SearchAsync(
                sessionId,
                keyword: "timeout",
                severity: "ERROR",
                sourceType: "CloudWatchLogs",
                startTime: null,
                endTime: null,
                limit: 10);

            records.Should().ContainSingle();
            records[0].Content.Should().Contain("timeout");
        }
        finally
        {
            var toDeleteDiagnostics = await dbContext.DiagnosticData
                .Where(d => diagnosticIds.Contains(d.Id))
                .ToListAsync();
            dbContext.DiagnosticData.RemoveRange(toDeleteDiagnostics);

            var toDeleteSession = await dbContext.Sessions
                .Where(s => s.Id == sessionId)
                .ToListAsync();
            dbContext.Sessions.RemoveRange(toDeleteSession);

            await dbContext.SaveChangesAsync();
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
