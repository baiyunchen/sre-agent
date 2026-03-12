using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SreAgent.Api.Controllers;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using Xunit;

namespace SreAgent.Api.Tests;

public class DashboardControllerTests
{
    private static DashboardController CreateController(
        ISessionRepository? sessionRepository = null,
        IAuditLogRepository? auditLogRepository = null)
    {
        return new DashboardController(
            sessionRepository ?? Mock.Of<ISessionRepository>(),
            auditLogRepository ?? Mock.Of<IAuditLogRepository>());
    }

    [Fact]
    public async Task GetStats_ShouldReturnMappedDashboardStats()
    {
        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetDashboardStatsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardStatsResult
            {
                TotalSessionsToday = 12,
                AutoResolutionRate = 66.67,
                AvgProcessingTimeSeconds = 124,
                PendingApprovals = 2
            });

        var controller = CreateController(sessionRepository: sessionRepository.Object);
        var result = await controller.GetStats(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<DashboardStatsResponse>().Subject;

        payload.TotalSessionsToday.Should().Be(12);
        payload.AutoResolutionRate.Should().Be(66.67);
        payload.AvgProcessingTimeSeconds.Should().Be(124);
        payload.PendingApprovals.Should().Be(2);
    }

    [Fact]
    public async Task GetActiveSessions_ShouldReturnBadRequest_WhenLimitIsInvalid()
    {
        var controller = CreateController();
        var result = await controller.GetActiveSessions(0, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetActiveSessions_ShouldReturnMappedItems()
    {
        var now = DateTime.UtcNow;
        var sessions = new List<SessionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AlertName = "inventory-service-log-errors-dev",
                ServiceName = "inventory-service",
                Status = "Running",
                CurrentStep = 3,
                StartedAt = now.AddMinutes(-5),
                UpdatedAt = now.AddMinutes(-1)
            }
        };

        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetActiveSessionsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((sessions, sessions.Count));

        var controller = CreateController(sessionRepository: sessionRepository.Object);
        var result = await controller.GetActiveSessions(10, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<DashboardActiveSessionsResponse>().Subject;

        payload.Total.Should().Be(1);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].AlertName.Should().Be("inventory-service-log-errors-dev");
        payload.Items[0].Status.Should().Be("Running");
    }

    [Fact]
    public async Task GetActivities_ShouldReturnBadRequest_WhenLimitIsInvalid()
    {
        var controller = CreateController();
        var result = await controller.GetActivities(0, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetActivities_ShouldReturnMappedItems()
    {
        var now = DateTime.UtcNow;
        var activities = new List<AuditLogEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                EventType = "SessionCompleted",
                EventDescription = "Analysis completed in 3200ms",
                Actor = "system",
                OccurredAt = now
            }
        };

        var auditLogRepository = new Mock<IAuditLogRepository>();
        auditLogRepository
            .Setup(r => r.GetRecentAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((activities, activities.Count));

        var controller = CreateController(auditLogRepository: auditLogRepository.Object);
        var result = await controller.GetActivities(20, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<DashboardActivitiesResponse>().Subject;

        payload.Total.Should().Be(1);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].EventType.Should().Be("SessionCompleted");
        payload.Items[0].Actor.Should().Be("system");
    }
}
