using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SreAgent.Repository;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using Xunit;

namespace SreAgent.Api.Tests;

public class AuditLogRepositoryIntegrationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=sre_agent;Username=sre_agent;Password=sre_agent";

    [Fact]
    public async Task GetRecentAsync_ShouldReturnLatestItemsByOccurredAt()
    {
        var marker = $"audit-it-{Guid.NewGuid():N}";
        var auditLogIds = new List<Guid>();
        var sessionId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        if (!await dbContext.Database.CanConnectAsync())
            return;

        await dbContext.Database.MigrateAsync();
        var repository = new AuditLogRepository(dbContext);
        var session = new SessionEntity
        {
            Id = sessionId,
            Status = "Running",
            AlertId = $"alert-{Guid.NewGuid():N}",
            AlertName = $"{marker}-alert",
            ServiceName = $"{marker}-service",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        await dbContext.Sessions.AddAsync(session);
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var logs = new[]
        {
            CreateAuditLog(sessionId, marker, "SessionStarted", now.AddMinutes(-3)),
            CreateAuditLog(sessionId, marker, "SessionCompleted", now.AddMinutes(-2)),
            CreateAuditLog(sessionId, marker, "SessionMessageSent", now.AddMinutes(-1))
        };

        auditLogIds.AddRange(logs.Select(l => l.Id));
        await dbContext.AuditLogs.AddRangeAsync(logs);
        await dbContext.SaveChangesAsync();

        try
        {
            var (items, total) = await repository.GetRecentAsync(2);

            total.Should().BeGreaterThanOrEqualTo(3);
            items.Should().HaveCount(2);
            items[0].OccurredAt.Should().BeAfter(items[1].OccurredAt);
            items.Select(i => i.EventDescription).Should().Contain(d => d != null && d.Contains(marker, StringComparison.Ordinal));
        }
        finally
        {
            var toDelete = await dbContext.AuditLogs.Where(a => auditLogIds.Contains(a.Id)).ToListAsync();
            dbContext.AuditLogs.RemoveRange(toDelete);
            var createdSession = await dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (createdSession != null)
                dbContext.Sessions.Remove(createdSession);
            await dbContext.SaveChangesAsync();
        }
    }

    private static AuditLogEntity CreateAuditLog(Guid sessionId, string marker, string eventType, DateTime occurredAt)
    {
        return new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            EventType = eventType,
            EventDescription = $"{marker}-{eventType}",
            Actor = "system",
            OccurredAt = occurredAt
        };
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("SRE_AGENT_TEST_DB_CONNECTION")
               ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
               ?? ConnectionString;
    }
}
