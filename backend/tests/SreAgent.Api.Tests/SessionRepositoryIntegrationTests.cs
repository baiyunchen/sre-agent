using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SreAgent.Repository;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using System.Text.Json;
using Xunit;

namespace SreAgent.Api.Tests;

public class SessionRepositoryIntegrationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=sre_agent;Username=sre_agent;Password=sre_agent";

    [Fact]
    public async Task ListAsync_ShouldSupportPagingAndStatusFilter()
    {
        var marker = $"repo-it-{Guid.NewGuid():N}";
        var ids = new List<Guid>();

        await using var dbContext = CreateDbContext();
        if (!await dbContext.Database.CanConnectAsync())
            return;

        await dbContext.Database.MigrateAsync();

        var now = DateTime.UtcNow;
        var seeded = new[]
        {
            CreateSession(marker, "Running", "CloudWatch", "Critical", now.AddMinutes(-3)),
            CreateSession(marker, "Running", "Prometheus", "Warning", now.AddMinutes(-2)),
            CreateSession(marker, "Completed", "CloudWatch", "Info", now.AddMinutes(-1))
        };

        ids.AddRange(seeded.Select(s => s.Id));
        await dbContext.Sessions.AddRangeAsync(seeded);
        await dbContext.SaveChangesAsync();

        try
        {
            var repository = new SessionRepository(dbContext);

            var pagedQuery = new SessionListQuery
            {
                Search = marker,
                Page = 1,
                PageSize = 2,
                Sort = "createdAt",
                SortOrder = "desc"
            };

            var (items, total) = await repository.ListAsync(pagedQuery);
            total.Should().Be(3);
            items.Should().HaveCount(2);

            var runningQuery = new SessionListQuery
            {
                Search = marker,
                Status = "Running",
                Page = 1,
                PageSize = 10
            };

            var (runningItems, runningTotal) = await repository.ListAsync(runningQuery);
            runningTotal.Should().Be(2);
            runningItems.Should().OnlyContain(s => s.Status == "Running");
        }
        finally
        {
            var toDelete = await dbContext.Sessions.Where(s => ids.Contains(s.Id)).ToListAsync();
            dbContext.Sessions.RemoveRange(toDelete);
            await dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ListAsync_ShouldSupportSourceFilterAndSearch()
    {
        var marker = $"repo-it-{Guid.NewGuid():N}";
        var ids = new List<Guid>();

        await using var dbContext = CreateDbContext();
        if (!await dbContext.Database.CanConnectAsync())
            return;

        await dbContext.Database.MigrateAsync();

        var seeded = new[]
        {
            CreateSession(marker, "Running", "CloudWatch", "Critical", DateTime.UtcNow.AddMinutes(-3)),
            CreateSession(marker, "Running", "Prometheus", "Critical", DateTime.UtcNow.AddMinutes(-2)),
            new SessionEntity
            {
                Id = Guid.NewGuid(),
                Status = "Running",
                AlertId = $"alert-{Guid.NewGuid():N}",
                AlertName = $"{marker}-alert",
                ServiceName = $"{marker}-service",
                AlertData = null,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new SessionEntity
            {
                Id = Guid.NewGuid(),
                Status = "Running",
                AlertId = $"alert-{Guid.NewGuid():N}",
                AlertName = $"{marker}-alert",
                ServiceName = $"{marker}-service",
                AlertData = JsonDocument.Parse("""{"source":123}"""),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        ids.AddRange(seeded.Select(s => s.Id));
        await dbContext.Sessions.AddRangeAsync(seeded);
        await dbContext.SaveChangesAsync();

        try
        {
            var repository = new SessionRepository(dbContext);
            var query = new SessionListQuery
            {
                Search = marker,
                Source = "CloudWatch",
                Page = 1,
                PageSize = 10
            };

            var (items, total) = await repository.ListAsync(query);

            total.Should().Be(1);
            items.Should().ContainSingle();
            items[0].AlertName.Should().Contain(marker);
            items[0].AlertData.Should().NotBeNull();
        }
        finally
        {
            var toDelete = await dbContext.Sessions.Where(s => ids.Contains(s.Id)).ToListAsync();
            dbContext.Sessions.RemoveRange(toDelete);
            await dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task CrudAndQueryMethods_ShouldWorkOnPersistedSession()
    {
        var marker = $"repo-it-{Guid.NewGuid():N}";

        await using var dbContext = CreateDbContext();
        if (!await dbContext.Database.CanConnectAsync())
            return;

        await dbContext.Database.MigrateAsync();
        var repository = new SessionRepository(dbContext);
        var createdAt = DateTime.UtcNow.AddMinutes(-5);
        var session = CreateSession(marker, "Created", "CloudWatch", "Warning", createdAt);
        session.AlertId = $"{marker}-alert-id";
        session.AlertData = JsonDocument.Parse($$"""{"alertSource":"CloudWatch","severity":"Warning"}""");

        await repository.CreateAsync(session);

        try
        {
            var loaded = await repository.GetAsync(session.Id);
            loaded.Should().NotBeNull();
            loaded!.Status.Should().Be("Created");

            loaded.Status = "Running";
            var beforeUpdate = loaded.UpdatedAt;
            await Task.Delay(5);
            await repository.UpdateAsync(loaded);

            var updated = await repository.GetAsync(session.Id);
            updated.Should().NotBeNull();
            updated!.Status.Should().Be("Running");
            updated.UpdatedAt.Should().BeAfter(beforeUpdate);

            var byStatus = await repository.GetByStatusAsync("Running");
            byStatus.Should().Contain(s => s.Id == session.Id);

            var byAlert = await repository.GetByAlertAsync(session.AlertId!);
            byAlert.Should().ContainSingle(s => s.Id == session.Id);

            var sourceByAlertSourceField = await repository.ListAsync(new SessionListQuery
            {
                Search = marker,
                Source = "CloudWatch",
                Sort = "updatedAt",
                SortOrder = "asc",
                Page = 1,
                PageSize = 10
            });

            sourceByAlertSourceField.Total.Should().Be(1);
            sourceByAlertSourceField.Items.Should().ContainSingle(s => s.Id == session.Id);

            var sortedByStatus = await repository.ListAsync(new SessionListQuery
            {
                Search = marker,
                Sort = "status",
                SortOrder = "asc",
                Page = 1,
                PageSize = 10
            });

            sortedByStatus.Total.Should().Be(1);
            sortedByStatus.Items.Should().ContainSingle(s => s.Id == session.Id);

            var sortedByUpdatedAtDesc = await repository.ListAsync(new SessionListQuery
            {
                Search = marker,
                Sort = "updatedAt",
                SortOrder = "desc",
                Page = 1,
                PageSize = 10
            });

            sortedByUpdatedAtDesc.Total.Should().Be(1);
            sortedByUpdatedAtDesc.Items.Should().ContainSingle(s => s.Id == session.Id);

            var sortedByCreatedAtAsc = await repository.ListAsync(new SessionListQuery
            {
                Search = marker,
                Sort = "createdAt",
                SortOrder = "asc",
                Page = 1,
                PageSize = 10
            });

            sortedByCreatedAtAsc.Total.Should().Be(1);
            sortedByCreatedAtAsc.Items.Should().ContainSingle(s => s.Id == session.Id);
        }
        finally
        {
            var toDelete = await dbContext.Sessions.Where(s => s.Id == session.Id).ToListAsync();
            dbContext.Sessions.RemoveRange(toDelete);
            await dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task DashboardQueries_ShouldReturnStatsAndActiveSessions()
    {
        var marker = $"repo-it-{Guid.NewGuid():N}";
        var ids = new List<Guid>();

        await using var dbContext = CreateDbContext();
        if (!await dbContext.Database.CanConnectAsync())
            return;

        await dbContext.Database.MigrateAsync();
        var repository = new SessionRepository(dbContext);

        var now = DateTime.UtcNow;
        var startOfDay = now.Date;
        var baselineStats = await repository.GetDashboardStatsAsync(startOfDay);

        var sessions = new[]
        {
            CreateSession(marker, "Running", "CloudWatch", "Warning", startOfDay.AddHours(1)),
            new SessionEntity
            {
                Id = Guid.NewGuid(),
                Status = "Completed",
                AlertId = $"alert-{Guid.NewGuid():N}",
                AlertName = $"{marker}-completed",
                AlertSource = "CloudWatch",
                AlertSeverity = "Critical",
                ServiceName = $"{marker}-service",
                CreatedAt = startOfDay.AddHours(2),
                StartedAt = startOfDay.AddHours(2),
                CompletedAt = startOfDay.AddHours(2).AddSeconds(120),
                UpdatedAt = startOfDay.AddHours(2).AddSeconds(120)
            },
            CreateSession(marker, "WaitingApproval", "CloudWatch", "Warning", startOfDay.AddHours(3)),
            CreateSession(marker, "Completed", "CloudWatch", "Warning", startOfDay.AddDays(-1).AddHours(10))
        };

        ids.AddRange(sessions.Select(s => s.Id));
        await dbContext.Sessions.AddRangeAsync(sessions);
        await dbContext.SaveChangesAsync();

        try
        {
            var stats = await repository.GetDashboardStatsAsync(startOfDay);
            stats.TotalSessionsToday.Should().BeGreaterThanOrEqualTo(baselineStats.TotalSessionsToday + 3);
            stats.PendingApprovals.Should().BeGreaterThanOrEqualTo(baselineStats.PendingApprovals + 1);

            var (activeItems, activeTotal) = await repository.GetActiveSessionsAsync(10);
            activeTotal.Should().BeGreaterThanOrEqualTo(2);
            activeItems.Should().Contain(item => item.Status == "Running" && item.AlertName == $"{marker}-alert");
            activeItems.Should().Contain(item => item.Status == "WaitingApproval");
        }
        finally
        {
            var toDelete = await dbContext.Sessions.Where(s => ids.Contains(s.Id)).ToListAsync();
            dbContext.Sessions.RemoveRange(toDelete);
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

    private static SessionEntity CreateSession(
        string marker,
        string status,
        string source,
        string severity,
        DateTime createdAt)
    {
        return new SessionEntity
        {
            Id = Guid.NewGuid(),
            Status = status,
            AlertId = $"alert-{Guid.NewGuid():N}",
            AlertName = $"{marker}-alert",
            AlertSource = source,
            AlertSeverity = severity,
            ServiceName = $"{marker}-service",
            AlertData = JsonDocument.Parse(
                $$"""{"source":"{{source}}","severity":"{{severity}}","marker":"{{marker}}"}"""),
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            CurrentStep = 1
        };
    }
}
