using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SreAgent.Api.Controllers;
using SreAgent.Application.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using System.Text.Json;
using Xunit;

namespace SreAgent.Api.Tests;

public class SessionControllerTests
{
    [Fact]
    public async Task GetSessions_ShouldReturnBadRequest_WhenPageIsInvalid()
    {
        var controller = CreateController(Mock.Of<ISessionRepository>());
        var request = new GetSessionsRequest { Page = 0 };

        var result = await controller.GetSessions(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSessions_ShouldReturnBadRequest_WhenSortIsInvalid()
    {
        var controller = CreateController(Mock.Of<ISessionRepository>());
        var request = new GetSessionsRequest { Sort = "bad-sort" };

        var result = await controller.GetSessions(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSessions_ShouldReturnOkWithMappedPayload_WhenRequestIsValid()
    {
        var sessionRepository = new Mock<ISessionRepository>();
        var session = new SessionEntity
        {
            Id = Guid.NewGuid(),
            Status = "Running",
            AlertId = "alert-001",
            AlertName = "CPU High",
            ServiceName = "payment-service",
            AlertData = JsonDocument.Parse("""{"source":"CloudWatch","severity":"Critical"}"""),
            CurrentStep = 3,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2),
            StartedAt = DateTime.UtcNow.AddMinutes(-8),
            CompletedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        sessionRepository
            .Setup(r => r.ListAsync(It.IsAny<SessionListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SessionEntity> { session }, 1));

        var controller = CreateController(sessionRepository.Object);
        var request = new GetSessionsRequest();

        var result = await controller.GetSessions(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionListResponse>().Subject;

        payload.Total.Should().Be(1);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].Source.Should().Be("CloudWatch");
        payload.Items[0].Severity.Should().Be("Critical");
        payload.Items[0].AgentSteps.Should().Be(3);
        payload.Items[0].Duration.Should().BeGreaterThan(0);
    }

    private static SessionController CreateController(ISessionRepository sessionRepository)
    {
        return new SessionController(
            sessionRepository,
            Mock.Of<ICheckpointService>(),
            Mock.Of<IInterventionService>(),
            Mock.Of<ISessionRecoveryService>(),
            Mock.Of<IAuditService>(),
            Mock.Of<IAgent>(),
            Mock.Of<ILogger<SessionController>>());
    }
}