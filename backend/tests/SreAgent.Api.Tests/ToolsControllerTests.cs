using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SreAgent.Api.Controllers;
using SreAgent.Application.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Options;
using SreAgent.Framework.Results;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using Xunit;

namespace SreAgent.Api.Tests;

public class ToolsControllerTests
{
    [Fact]
    public async Task GetTools_ShouldReturnToolRegistryWithStatsAndApprovalMode()
    {
        var tool = new FakeTool("cloudwatch_simple_query", "Query CloudWatch logs", "Observability");
        var controller = CreateController(
            tools: [tool],
            stats: [
                new ToolInvocationStats
                {
                    ToolName = tool.Name,
                    Invocations = 10,
                    SuccessCount = 9,
                    AvgDurationMs = 1500
                }
            ],
            rules: [
                new ApprovalRuleEntity
                {
                    Id = Guid.NewGuid(),
                    ToolName = tool.Name,
                    RuleType = "require-approval",
                    CreatedAt = DateTime.UtcNow
                }
            ]);

        var result = await controller.GetTools(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ToolRegistryResponse>().Subject;
        payload.Total.Should().Be(1);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].Name.Should().Be(tool.Name);
        payload.Items[0].AutoApprove.Should().BeFalse();
        payload.Items[0].ApprovalMode.Should().Be("require-approval");
        payload.Items[0].Invocations.Should().Be(10);
        payload.Items[0].SuccessRate.Should().Be(90);
        payload.Items[0].AvgDurationMs.Should().Be(1500);
    }

    [Fact]
    public async Task UpdateToolApprovalMode_ShouldReturnNotFound_WhenToolDoesNotExist()
    {
        var controller = CreateController(tools: []);
        var result = await controller.UpdateToolApprovalMode(
            "missing_tool",
            new UpdateToolApprovalModeRequest { AutoApprove = true },
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateToolApprovalMode_ShouldReturnBadRequest_WhenBodyIsNull()
    {
        var tool = new FakeTool("todo_write", "Write todos", "Task");
        var controller = CreateController(tools: [tool]);

        var result = await controller.UpdateToolApprovalMode(tool.Name, null, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateToolApprovalMode_ShouldUpsertRuleAndReturnUpdatedItem()
    {
        var tool = new FakeTool("todo_write", "Write todos", "Task");

        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.UpsertRuleAsync(tool.Name, "always-allow", "oncall", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalRuleEntity
            {
                Id = Guid.NewGuid(),
                ToolName = tool.Name,
                RuleType = "always-allow",
                CreatedBy = "oncall",
                CreatedAt = DateTime.UtcNow
            });
        approvalService
            .Setup(s => s.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var invocationRepo = new Mock<IToolInvocationRepository>();
        invocationRepo
            .Setup(r => r.GetStatsByToolNamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ToolInvocationStats
                {
                    ToolName = tool.Name,
                    Invocations = 5,
                    SuccessCount = 5,
                    AvgDurationMs = 600
                }
            ]);

        var controller = new ToolsController(
            CreateAgent([tool]),
            approvalService.Object,
            invocationRepo.Object);

        var result = await controller.UpdateToolApprovalMode(
            tool.Name,
            new UpdateToolApprovalModeRequest
            {
                AutoApprove = true,
                UpdatedBy = "oncall"
            },
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ToolRegistryItem>().Subject;
        payload.Name.Should().Be(tool.Name);
        payload.AutoApprove.Should().BeTrue();
        payload.ApprovalMode.Should().Be("auto-approve");
    }

    private static ToolsController CreateController(
        IReadOnlyList<ITool> tools,
        IReadOnlyList<ToolInvocationStats>? stats = null,
        IReadOnlyList<ApprovalRuleEntity>? rules = null)
    {
        var approvalService = new Mock<IApprovalService>();
        approvalService
            .Setup(s => s.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules ?? []);
        approvalService
            .Setup(s => s.UpsertRuleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string toolName, string ruleType, string? createdBy, CancellationToken _) => new ApprovalRuleEntity
            {
                Id = Guid.NewGuid(),
                ToolName = toolName,
                RuleType = ruleType,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            });

        var invocationRepo = new Mock<IToolInvocationRepository>();
        invocationRepo
            .Setup(r => r.GetStatsByToolNamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats ?? []);

        return new ToolsController(CreateAgent(tools), approvalService.Object, invocationRepo.Object);
    }

    private static IAgent CreateAgent(IReadOnlyList<ITool> tools)
    {
        var agent = new Mock<IAgent>();
        agent.SetupGet(a => a.Options).Returns(new AgentOptions { Tools = tools });
        return agent.Object;
    }

    private sealed class FakeTool : ITool
    {
        public FakeTool(string name, string summary, string category)
        {
            Name = name;
            Summary = summary;
            Category = category;
        }

        public string Name { get; }
        public string Summary { get; }
        public string Description => Summary;
        public string Category { get; }

        public ToolDetail GetDetail()
        {
            return new ToolDetail
            {
                Name = Name,
                Description = Description,
                ParameterSchema = "{}"
            };
        }

        public Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolResult.Success("ok"));
        }
    }
}
