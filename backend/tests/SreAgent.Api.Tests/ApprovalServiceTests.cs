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

        var service = CreateService(sessionRepository.Object, interventionRepository.Object, auditService: auditService.Object);
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

        var service = CreateService(sessionRepository.Object, interventionRepository.Object, auditService: auditService.Object);
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

    [Fact]
    public async Task GetRulesAsync_ShouldDelegateToRepository()
    {
        var rules = new List<ApprovalRuleEntity>
        {
            new() { Id = Guid.NewGuid(), ToolName = "kubectl_delete_pod", RuleType = "always-allow" }
        };

        var ruleRepository = new Mock<IApprovalRuleRepository>();
        ruleRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var service = CreateService(approvalRuleRepository: ruleRepository.Object);
        var result = await service.GetRulesAsync(CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].ToolName.Should().Be("kubectl_delete_pod");
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldCreateWithValidInput()
    {
        var ruleRepository = new Mock<IApprovalRuleRepository>();
        ruleRepository
            .Setup(r => r.CreateAsync(It.IsAny<ApprovalRuleEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApprovalRuleEntity rule, CancellationToken _) => rule);

        var service = CreateService(approvalRuleRepository: ruleRepository.Object);
        var result = await service.CreateRuleAsync("kubectl_delete_pod", "always-allow", "admin", CancellationToken.None);

        result.ToolName.Should().Be("kubectl_delete_pod");
        result.RuleType.Should().Be("always-allow");
        result.CreatedBy.Should().Be("admin");
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldThrow_WhenToolNameEmpty()
    {
        var service = CreateService();
        var act = async () => await service.CreateRuleAsync("", "always-allow", null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldThrow_WhenRuleTypeInvalid()
    {
        var service = CreateService();
        var act = async () => await service.CreateRuleAsync("tool", "invalid-type", null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteRuleAsync_ShouldDelegateToRepository()
    {
        var ruleId = Guid.NewGuid();
        var ruleRepository = new Mock<IApprovalRuleRepository>();
        ruleRepository
            .Setup(r => r.DeleteAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(approvalRuleRepository: ruleRepository.Object);
        var result = await service.DeleteRuleAsync(ruleId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertRuleAsync_ShouldDelegateToRepository()
    {
        var updatedAt = DateTime.UtcNow;
        var ruleRepository = new Mock<IApprovalRuleRepository>();
        ruleRepository
            .Setup(r => r.UpsertByToolNameAsync("todo_write", "require-approval", "oncall", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalRuleEntity
            {
                Id = Guid.NewGuid(),
                ToolName = "todo_write",
                RuleType = "require-approval",
                CreatedBy = "oncall",
                CreatedAt = updatedAt
            });

        var service = CreateService(approvalRuleRepository: ruleRepository.Object);
        var result = await service.UpsertRuleAsync("todo_write", "require-approval", "oncall", CancellationToken.None);

        result.ToolName.Should().Be("todo_write");
        result.RuleType.Should().Be("require-approval");
    }

    [Fact]
    public async Task UpsertRuleAsync_ShouldThrow_WhenRuleTypeInvalid()
    {
        var service = CreateService();
        var act = async () => await service.UpsertRuleAsync("tool", "auto-approve", null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static ApprovalService CreateService(
        ISessionRepository? sessionRepository = null,
        IInterventionRepository? interventionRepository = null,
        IApprovalRuleRepository? approvalRuleRepository = null,
        IAuditService? auditService = null)
    {
        return new ApprovalService(
            sessionRepository ?? Mock.Of<ISessionRepository>(),
            interventionRepository ?? Mock.Of<IInterventionRepository>(),
            approvalRuleRepository ?? Mock.Of<IApprovalRuleRepository>(),
            auditService ?? Mock.Of<IAuditService>());
    }
}
