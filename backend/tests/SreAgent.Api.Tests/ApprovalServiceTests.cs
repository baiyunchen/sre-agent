using FluentAssertions;
using Moq;
using SreAgent.Application.Services;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using Xunit;

namespace SreAgent.Api.Tests;

public class ApprovalServiceTests
{
    [Fact]
    public async Task GetPendingAsync_ShouldReturnLimitedItems()
    {
        var sessions = new List<SessionEntity>
        {
            new() { Id = Guid.NewGuid(), Status = "WaitingApproval" },
            new() { Id = Guid.NewGuid(), Status = "WaitingApproval" }
        };

        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetByStatusAsync("WaitingApproval", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var service = CreateService(sessionRepository: sessionRepository.Object);
        var (items, total) = await service.GetPendingAsync(1, CancellationToken.None);

        total.Should().Be(2);
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApproveAsync_ShouldUpdateStatusAndRecordIntervention()
    {
        var sessionId = Guid.NewGuid();
        var session = new SessionEntity
        {
            Id = sessionId,
            Status = "WaitingApproval"
        };

        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var interventionRepository = new Mock<IInterventionRepository>();
        var auditService = new Mock<IAuditService>();

        var service = CreateService(sessionRepository.Object, interventionRepository.Object, auditService.Object);
        var result = await service.ApproveAsync(sessionId, "approver", "ok", CancellationToken.None);

        result.Status.Should().Be("Running");
        session.Status.Should().Be("Running");
        sessionRepository.Verify(r => r.UpdateAsync(It.Is<SessionEntity>(s => s.Status == "Running"), It.IsAny<CancellationToken>()), Times.Once);
        interventionRepository.Verify(r => r.CreateAsync(
            It.Is<InterventionEntity>(i => i.Type == "Approve" && i.IntervenedBy == "approver"),
            It.IsAny<CancellationToken>()), Times.Once);
        auditService.Verify(a => a.LogAsync(
            sessionId,
            "SessionApproved",
            It.IsAny<string>(),
            It.IsAny<object?>(),
            "approver",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_ShouldThrow_WhenSessionStatusIsNotWaitingApproval()
    {
        var sessionId = Guid.NewGuid();
        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity
            {
                Id = sessionId,
                Status = "Running"
            });

        var service = CreateService(sessionRepository: sessionRepository.Object);
        var act = async () => await service.RejectAsync(sessionId, "approver", null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RejectAsync_ShouldUpdateStatusAndRecordIntervention()
    {
        var sessionId = Guid.NewGuid();
        var session = new SessionEntity
        {
            Id = sessionId,
            Status = "WaitingApproval"
        };

        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var interventionRepository = new Mock<IInterventionRepository>();
        var auditService = new Mock<IAuditService>();

        var service = CreateService(sessionRepository.Object, interventionRepository.Object, auditService.Object);
        var result = await service.RejectAsync(sessionId, "approver", "not safe", CancellationToken.None);

        result.Status.Should().Be("Cancelled");
        session.Status.Should().Be("Cancelled");
        sessionRepository.Verify(r => r.UpdateAsync(It.Is<SessionEntity>(s => s.Status == "Cancelled"), It.IsAny<CancellationToken>()), Times.Once);
        interventionRepository.Verify(r => r.CreateAsync(
            It.Is<InterventionEntity>(i => i.Type == "Reject" && i.IntervenedBy == "approver"),
            It.IsAny<CancellationToken>()), Times.Once);
        auditService.Verify(a => a.LogAsync(
            sessionId,
            "SessionRejected",
            It.IsAny<string>(),
            It.IsAny<object?>(),
            "approver",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldDelegateToRepository()
    {
        var interventions = new List<InterventionEntity>
        {
            new() { Id = Guid.NewGuid(), Type = "Approve", IntervenedAt = DateTime.UtcNow }
        };

        var interventionRepository = new Mock<IInterventionRepository>();
        interventionRepository
            .Setup(r => r.GetByTypesAsync(It.IsAny<IReadOnlyCollection<string>>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((interventions, interventions.Count));

        var service = CreateService(interventionRepository: interventionRepository.Object);
        var (items, total) = await service.GetHistoryAsync(10, CancellationToken.None);

        total.Should().Be(1);
        items.Should().HaveCount(1);
    }

    private static ApprovalService CreateService(
        ISessionRepository? sessionRepository = null,
        IInterventionRepository? interventionRepository = null,
        IAuditService? auditService = null)
    {
        return new ApprovalService(
            sessionRepository ?? Mock.Of<ISessionRepository>(),
            interventionRepository ?? Mock.Of<IInterventionRepository>(),
            auditService ?? Mock.Of<IAuditService>());
    }
}
