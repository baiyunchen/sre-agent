using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SreAgent.Repository;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using Xunit;

namespace SreAgent.Api.Tests;

public class InterventionRepositoryIntegrationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=sre_agent;Username=sre_agent;Password=sre_agent";

    [Fact]
    public async Task GetByTypesAsync_ShouldReturnFilteredApprovalHistory()
    {
        var marker = $"intervention-it-{Guid.NewGuid():N}";
        var sessionId = Guid.NewGuid();
        var interventionIds = new List<Guid>();

        await using var dbContext = CreateDbContext();
        if (!await dbContext.Database.CanConnectAsync())
            return;

        await dbContext.Database.MigrateAsync();
        var repository = new InterventionRepository(dbContext);

        var session = new SessionEntity
        {
            Id = sessionId,
            Status = "WaitingApproval",
            AlertId = $"alert-{Guid.NewGuid():N}",
            AlertName = $"{marker}-alert",
            ServiceName = $"{marker}-service",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        await dbContext.Sessions.AddAsync(session);
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var interventions = new[]
        {
            new InterventionEntity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Type = "Approve",
                Reason = $"{marker}-approve",
                IntervenedBy = "oncall",
                IntervenedAt = now.AddMinutes(-2)
            },
            new InterventionEntity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Type = "Reject",
                Reason = $"{marker}-reject",
                IntervenedBy = "oncall",
                IntervenedAt = now.AddMinutes(-1)
            },
            new InterventionEntity
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Type = "Cancel",
                Reason = $"{marker}-cancel",
                IntervenedBy = "system",
                IntervenedAt = now
            }
        };

        interventionIds.AddRange(interventions.Select(i => i.Id));
        await dbContext.Interventions.AddRangeAsync(interventions);
        await dbContext.SaveChangesAsync();

        try
        {
            var (items, total) = await repository.GetByTypesAsync(["Approve", "Reject"], 10);

            total.Should().BeGreaterThanOrEqualTo(2);
            items.Should().OnlyContain(i => i.Type == "Approve" || i.Type == "Reject");
            items.Should().Contain(i => i.Reason == $"{marker}-approve");
            items.Should().Contain(i => i.Reason == $"{marker}-reject");
            items.First().IntervenedAt.Should().BeOnOrAfter(items.Last().IntervenedAt);
        }
        finally
        {
            var storedInterventions = await dbContext.Interventions
                .Where(i => interventionIds.Contains(i.Id))
                .ToListAsync();
            dbContext.Interventions.RemoveRange(storedInterventions);

            var storedSession = await dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (storedSession != null)
                dbContext.Sessions.Remove(storedSession);

            await dbContext.SaveChangesAsync();
        }
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
