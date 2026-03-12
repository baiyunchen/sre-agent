using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SreAgent.Api.Controllers;
using SreAgent.Application.Services;
using SreAgent.Repository.Entities;
using Xunit;

namespace SreAgent.Api.Tests;

public class ApprovalsControllerTests
{
    [Fact]
    public async Task GetPending_ShouldReturnBadRequest_WhenLimitIsInvalid()
    {
        var controller = new ApprovalsController(Mock.Of<IApprovalService>());
        var result = await controller.GetPending(0, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPending_ShouldReturnMappedPayload()
    {
        var sessions = new List<SessionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AlertName = "inventory-service-log-errors-dev",
                ServiceName = "inventory-service",
                Status = "WaitingApproval",
                UpdatedAt = DateTime.UtcNow
            }
        };

        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.GetPendingAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((sessions, sessions.Count));

        var controller = new ApprovalsController(approvalService.Object);
        var result = await controller.GetPending(20, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApprovalPendingListResponse>().Subject;
        payload.Total.Should().Be(1);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].Status.Should().Be("WaitingApproval");
    }

    [Fact]
    public async Task Approve_ShouldReturnBadRequest_WhenApproverIdIsEmpty()
    {
        var controller = new ApprovalsController(Mock.Of<IApprovalService>());
        var result = await controller.Approve(Guid.NewGuid(), new ApprovalDecisionRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Approve_ShouldReturnNotFound_WhenSessionDoesNotExist()
    {
        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.ApproveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var controller = new ApprovalsController(approvalService.Object);
        var result = await controller.Approve(Guid.NewGuid(), new ApprovalDecisionRequest
        {
            ApproverId = "approver"
        }, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Approve_ShouldReturnMappedResponse()
    {
        var sessionId = Guid.NewGuid();
        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.ApproveAsync(sessionId, "approver", "looks good", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalDecisionResult
            {
                SessionId = sessionId,
                Status = "Running",
                Message = "Approval accepted"
            });

        var controller = new ApprovalsController(approvalService.Object);
        var result = await controller.Approve(sessionId, new ApprovalDecisionRequest
        {
            ApproverId = "approver",
            Comment = "looks good"
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApprovalDecisionResponse>().Subject;
        payload.SessionId.Should().Be(sessionId);
        payload.Status.Should().Be("Running");
    }

    [Fact]
    public async Task Reject_ShouldReturnBadRequest_WhenServiceThrowsInvalidOperation()
    {
        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.RejectAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("status mismatch"));

        var controller = new ApprovalsController(approvalService.Object);
        var result = await controller.Reject(Guid.NewGuid(), new ApprovalDecisionRequest
        {
            ApproverId = "approver"
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Reject_ShouldReturnNotFound_WhenSessionDoesNotExist()
    {
        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.RejectAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var controller = new ApprovalsController(approvalService.Object);
        var result = await controller.Reject(Guid.NewGuid(), new ApprovalDecisionRequest
        {
            ApproverId = "approver"
        }, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Reject_ShouldReturnMappedResponse()
    {
        var sessionId = Guid.NewGuid();
        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.RejectAsync(sessionId, "approver", "rollback", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalDecisionResult
            {
                SessionId = sessionId,
                Status = "Cancelled",
                Message = "Rejection accepted"
            });

        var controller = new ApprovalsController(approvalService.Object);
        var result = await controller.Reject(sessionId, new ApprovalDecisionRequest
        {
            ApproverId = "approver",
            Comment = "rollback"
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApprovalDecisionResponse>().Subject;
        payload.SessionId.Should().Be(sessionId);
        payload.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task GetHistory_ShouldReturnMappedPayload()
    {
        var history = new List<InterventionEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                Type = "Approve",
                Reason = "approved by oncall",
                IntervenedBy = "oncall",
                IntervenedAt = DateTime.UtcNow
            }
        };

        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.GetHistoryAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((history, history.Count));

        var controller = new ApprovalsController(approvalService.Object);
        var result = await controller.GetHistory(50, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApprovalHistoryResponse>().Subject;
        payload.Total.Should().Be(1);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].Action.Should().Be("Approve");
    }

    [Fact]
    public async Task GetHistory_ShouldReturnBadRequest_WhenLimitIsInvalid()
    {
        var controller = new ApprovalsController(Mock.Of<IApprovalService>());
        var result = await controller.GetHistory(0, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
