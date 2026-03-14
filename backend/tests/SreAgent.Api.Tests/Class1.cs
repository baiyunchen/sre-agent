using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SreAgent.Api.Controllers;
using SreAgent.Application.Services;
using SreAgent.Application.Tools.Todo.Models;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;
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
        var agentRunRepository = new Mock<IAgentRunRepository>();
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

        agentRunRepository
            .Setup(r => r.CountToolInvocationsBySessionsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [session.Id] = 7 });

        var controller = CreateController(sessionRepository.Object, agentRunRepository: agentRunRepository.Object);
        var request = new GetSessionsRequest();

        var result = await controller.GetSessions(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionListResponse>().Subject;

        payload.Total.Should().Be(1);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].Source.Should().Be("CloudWatch");
        payload.Items[0].Severity.Should().Be("Critical");
        payload.Items[0].Status.Should().Be("Completed");
        payload.Items[0].AgentSteps.Should().Be(7);
        payload.Items[0].Duration.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSession_ShouldReturnNotFound_WhenSessionDoesNotExist()
    {
        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionEntity?)null);

        var controller = CreateController(sessionRepository.Object);
        var result = await controller.GetSession(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSession_ShouldReturnDetailPayload_WithSourceAndSeverityFields()
    {
        var sessionRepository = new Mock<ISessionRepository>();
        var agentRunRepository = new Mock<IAgentRunRepository>();
        var session = new SessionEntity
        {
            Id = Guid.NewGuid(),
            Status = "Completed",
            AlertId = "alert-xyz",
            AlertName = "DB latency high",
            AlertSource = "Prometheus",
            AlertSeverity = "Warning",
            ServiceName = "db-service",
            CurrentAgentId = "SreCoordinator",
            CurrentStep = 4,
            DiagnosisSummary = "network jitter",
            Confidence = 0.82,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            StartedAt = DateTime.UtcNow.AddMinutes(-18),
            CompletedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-9)
        };

        sessionRepository
            .Setup(r => r.GetAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        agentRunRepository
            .Setup(r => r.CountToolInvocationsBySessionsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [session.Id] = 12 });

        var controller = CreateController(sessionRepository.Object, agentRunRepository: agentRunRepository.Object);
        var result = await controller.GetSession(session.Id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionDetailResponse>().Subject;

        payload.Id.Should().Be(session.Id);
        payload.Source.Should().Be("Prometheus");
        payload.Severity.Should().Be("Warning");
        payload.AgentSteps.Should().Be(12);
        payload.Duration.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSessionTimeline_ShouldReturnNotFound_WhenSessionDoesNotExist()
    {
        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionEntity?)null);

        var controller = CreateController(sessionRepository.Object);
        var result = await controller.GetSessionTimeline(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSessionTimeline_ShouldReturnMergedAndOrderedEvents()
    {
        var sessionId = Guid.NewGuid();
        var t1 = DateTime.UtcNow.AddMinutes(-10);
        var t2 = DateTime.UtcNow.AddMinutes(-9);
        var t3 = DateTime.UtcNow.AddMinutes(-8);
        var t4 = DateTime.UtcNow.AddMinutes(-7);

        var sessionRepository = new Mock<ISessionRepository>();
        var messageRepository = new Mock<IMessageRepository>();
        var agentRunRepository = new Mock<IAgentRunRepository>();

        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity { Id = sessionId, Status = "Running" });

        messageRepository
            .Setup(r => r.GetBySessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new MessageEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    Role = "User",
                    Parts = JsonDocument.Parse("""[{"text":"first message"}]"""),
                    CreatedAt = t1
                },
                new MessageEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    Role = "Assistant",
                    Parts = JsonDocument.Parse("""[{"text":"second message"}]"""),
                    CreatedAt = t3
                }
            ]);

        agentRunRepository
            .Setup(r => r.GetBySessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AgentRunEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    AgentId = "sre-coordinator",
                    AgentName = "SreCoordinator",
                    Status = "Completed",
                    StartedAt = t2,
                    ToolInvocations =
                    [
                        new ToolInvocationEntity
                        {
                            Id = Guid.NewGuid(),
                            AgentRunId = Guid.NewGuid(),
                            ToolName = "cloudwatch_simple_query",
                            Status = "Completed",
                            RequestedAt = t4
                        }
                    ]
                }
            ]);

        var controller = CreateController(sessionRepository.Object, messageRepository.Object, agentRunRepository.Object);
        var result = await controller.GetSessionTimeline(sessionId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionTimelineResponse>().Subject;

        payload.SessionId.Should().Be(sessionId);
        payload.Events.Should().HaveCount(4);
        payload.Events.Select(e => e.EventType).Should().ContainInOrder("message", "agent_run", "message", "tool_invocation");
    }

    [Fact]
    public async Task GetSessionDiagnosis_ShouldReturnStructuredPayload()
    {
        var sessionId = Guid.NewGuid();
        var sessionRepository = new Mock<ISessionRepository>();
        var diagnosticRepository = new Mock<IDiagnosticDataRepository>();

        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity
            {
                Id = sessionId,
                Status = "Completed",
                DiagnosisSummary = "库存查询失败导致告警持续触发",
                Confidence = 0.78
            });

        diagnosticRepository
            .Setup(r => r.GetSummaryAsync(sessionId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticSummaryResult
            {
                TotalRecords = 2,
                BySeverity = new Dictionary<string, int> { ["ERROR"] = 2 },
                BySource = new Dictionary<string, int> { ["CloudWatchLogs"] = 2 },
                EarliestTimestamp = DateTime.UtcNow.AddMinutes(-5),
                LatestTimestamp = DateTime.UtcNow
            });

        diagnosticRepository
            .Setup(r => r.SearchAsync(
                sessionId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DiagnosticDataEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    SourceType = "CloudWatchLogs",
                    Content = "Stock lookup failed for key undefined-PROD001"
                }
            ]);

        var controller = CreateController(sessionRepository.Object, diagnosticDataRepository: diagnosticRepository.Object);
        var result = await controller.GetSessionDiagnosis(sessionId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionDiagnosisResponse>().Subject;

        payload.SessionId.Should().Be(sessionId);
        payload.Hypothesis.Should().Contain("库存查询失败");
        payload.Confidence.Should().Be(0.78);
        payload.TotalRecords.Should().Be(2);
        payload.Evidence.Should().ContainSingle(item => item.Contains("CloudWatchLogs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetSessionDiagnosis_ShouldUseStructuredDiagnosisJson_WhenAvailable()
    {
        var sessionId = Guid.NewGuid();
        var sessionRepository = new Mock<ISessionRepository>();
        var diagnosticRepository = new Mock<IDiagnosticDataRepository>();

        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity
            {
                Id = sessionId,
                Status = "Completed",
                Diagnosis = JsonDocument.Parse(
                    """{"hypothesis":"数据库连接池耗尽","confidence":0.91,"evidence":["连接数持续增长"],"recommendedActions":["增加连接池上限"]}""")
            });

        diagnosticRepository
            .Setup(r => r.GetSummaryAsync(sessionId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticSummaryResult());

        diagnosticRepository
            .Setup(r => r.SearchAsync(
                sessionId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DiagnosticDataEntity>());

        var controller = CreateController(sessionRepository.Object, diagnosticDataRepository: diagnosticRepository.Object);
        var result = await controller.GetSessionDiagnosis(sessionId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionDiagnosisResponse>().Subject;

        payload.Hypothesis.Should().Be("数据库连接池耗尽");
        payload.Confidence.Should().Be(0.91);
        payload.Evidence.Should().Contain("连接数持续增长");
        payload.RecommendedActions.Should().Contain("增加连接池上限");
    }

    [Fact]
    public async Task GetSessionDiagnosis_ShouldExtractRecommendedActionsFromSummaryBullets()
    {
        var sessionId = Guid.NewGuid();
        var sessionRepository = new Mock<ISessionRepository>();
        var diagnosticRepository = new Mock<IDiagnosticDataRepository>();

        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity
            {
                Id = sessionId,
                Status = "Completed",
                DiagnosisSummary = """
                    分析结论
                    - 扩容库存服务实例
                    - 增加参数校验并发布补丁
                    """
            });

        diagnosticRepository
            .Setup(r => r.GetSummaryAsync(sessionId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticSummaryResult());

        diagnosticRepository
            .Setup(r => r.SearchAsync(
                sessionId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DiagnosticDataEntity>());

        var controller = CreateController(sessionRepository.Object, diagnosticDataRepository: diagnosticRepository.Object);
        var result = await controller.GetSessionDiagnosis(sessionId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionDiagnosisResponse>().Subject;

        payload.RecommendedActions.Should().ContainInOrder("扩容库存服务实例", "增加参数校验并发布补丁");
    }

    [Fact]
    public async Task GetSessionToolInvocations_ShouldReturnFlattenedItems()
    {
        var sessionId = Guid.NewGuid();
        var t1 = DateTime.UtcNow.AddMinutes(-3);
        var t2 = DateTime.UtcNow.AddMinutes(-1);

        var sessionRepository = new Mock<ISessionRepository>();
        var agentRunRepository = new Mock<IAgentRunRepository>();

        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity { Id = sessionId, Status = "Completed" });

        agentRunRepository
            .Setup(r => r.GetBySessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AgentRunEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    AgentId = "sre-coordinator",
                    AgentName = "SreCoordinator",
                    ToolInvocations =
                    [
                        new ToolInvocationEntity
                        {
                            Id = Guid.NewGuid(),
                            AgentRunId = Guid.NewGuid(),
                            ToolName = "cloudwatch_simple_query",
                            Status = "Completed",
                            RequestedAt = t1
                        },
                        new ToolInvocationEntity
                        {
                            Id = Guid.NewGuid(),
                            AgentRunId = Guid.NewGuid(),
                            ToolName = "knowledge_base_query",
                            Status = "Failed",
                            ErrorMessage = "timeout",
                            RequestedAt = t2
                        }
                    ]
                }
            ]);

        var controller = CreateController(sessionRepository.Object, agentRunRepository: agentRunRepository.Object);
        var result = await controller.GetSessionToolInvocations(sessionId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionToolInvocationsResponse>().Subject;

        payload.Items.Should().HaveCount(2);
        payload.Items[0].ToolName.Should().Be("knowledge_base_query");
        payload.Items[0].ErrorMessage.Should().Be("timeout");
        payload.Items[1].ToolName.Should().Be("cloudwatch_simple_query");
    }

    [Fact]
    public async Task GetSessionTodos_ShouldReturnMappedTodos()
    {
        var sessionId = Guid.NewGuid();
        var sessionRepository = new Mock<ISessionRepository>();
        var todoService = new Mock<ITodoService>();

        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity { Id = sessionId, Status = "Running" });

        todoService
            .Setup(s => s.GetAsync(sessionId))
            .ReturnsAsync(
            [
                new TodoItem
                {
                    Id = "t-1",
                    Content = "分析错误日志",
                    Priority = TodoPriority.High,
                    Status = TodoStatus.InProgress,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2)
                },
                new TodoItem
                {
                    Id = "t-2",
                    Content = "输出修复建议",
                    Priority = TodoPriority.Medium,
                    Status = TodoStatus.Completed,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                    CompletedAt = DateTime.UtcNow
                },
                new TodoItem
                {
                    Id = "t-3",
                    Content = "忽略已失效告警",
                    Priority = TodoPriority.Low,
                    Status = TodoStatus.Cancelled,
                    CreatedAt = DateTime.UtcNow
                }
            ]);

        var controller = CreateController(sessionRepository.Object, todoService: todoService.Object);
        var result = await controller.GetSessionTodos(sessionId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionTodosResponse>().Subject;

        payload.SessionId.Should().Be(sessionId);
        payload.Items.Should().HaveCount(3);
        payload.Items[0].Status.Should().Be("in_progress");
        payload.Items[0].Priority.Should().Be("high");
        payload.Items[1].Status.Should().Be("completed");
        payload.Items[2].Status.Should().Be("cancelled");
        payload.Items[2].Priority.Should().Be("low");
    }

    [Fact]
    public async Task GetSessionDiagnosis_ShouldReturnNotFound_WhenSessionDoesNotExist()
    {
        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionEntity?)null);

        var controller = CreateController(sessionRepository.Object);
        var result = await controller.GetSessionDiagnosis(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PostSessionMessage_ShouldReturnBadRequest_WhenMessageIsEmpty()
    {
        var controller = CreateController(Mock.Of<ISessionRepository>());

        var result = await controller.PostSessionMessage(
            Guid.NewGuid(),
            new SessionMessageRequest { Message = "   " },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostSessionMessage_ShouldReturnNotFound_WhenSessionDoesNotExist()
    {
        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionEntity?)null);

        var controller = CreateController(sessionRepository.Object);
        var result = await controller.PostSessionMessage(
            Guid.NewGuid(),
            new SessionMessageRequest { Message = "继续分析" },
            CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PostSessionMessage_ShouldReturnConflict_WhenSessionIsNotRunning()
    {
        var sessionId = Guid.NewGuid();
        var sessionRepository = new Mock<ISessionRepository>();
        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity { Id = sessionId, Status = "Completed" });

        var controller = CreateController(sessionRepository.Object);
        var result = await controller.PostSessionMessage(
            sessionId,
            new SessionMessageRequest { Message = "继续分析" },
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task PostSessionMessage_ShouldReturnConflict_WhenContextIsUnavailable()
    {
        var sessionId = Guid.NewGuid();
        var sessionRepository = new Mock<ISessionRepository>();
        var contextStore = new Mock<IContextStore>();

        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity { Id = sessionId, Status = "Running" });

        contextStore
            .Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextSnapshot?)null);

        var controller = CreateController(sessionRepository.Object, contextStore: contextStore.Object);
        var result = await controller.PostSessionMessage(
            sessionId,
            new SessionMessageRequest { Message = "继续分析" },
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task PostSessionMessage_ShouldReturnResponseAndPersistSnapshot_WhenRequestIsValid()
    {
        var sessionId = Guid.NewGuid();
        var sessionRepository = new Mock<ISessionRepository>();
        var contextStore = new Mock<IContextStore>();
        var auditService = new Mock<IAuditService>();
        var agent = new Mock<IAgent>();

        sessionRepository
            .Setup(r => r.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionEntity { Id = sessionId, Status = "Running" });

        contextStore
            .Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContextSnapshot.Empty(sessionId));
        contextStore
            .Setup(s => s.SaveAsync(It.IsAny<ContextSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        auditService
            .Setup(a => a.LogAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        agent.SetupGet(a => a.Id).Returns("sre-coordinator");
        agent
            .Setup(a => a.ExecuteAsync(
                It.IsAny<IContextManager>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IContextManager context, IReadOnlyDictionary<string, object>? _, CancellationToken _) =>
                AgentResult.Success("收到，继续分析中", context, new TokenUsage(7, 11), iterationCount: 2));

        var controller = CreateController(
            sessionRepository.Object,
            contextStore: contextStore.Object,
            auditService: auditService.Object,
            agent: agent.Object,
            tokenEstimator: new SimpleTokenEstimator());

        var result = await controller.PostSessionMessage(
            sessionId,
            new SessionMessageRequest { Message = "继续分析这个会话", UserId = "user-1" },
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<SessionMessageResponse>().Subject;

        payload.SessionId.Should().Be(sessionId);
        payload.IsSuccess.Should().BeTrue();
        payload.Output.Should().Contain("继续分析");
        payload.TokenUsage.TotalTokens.Should().Be(18);

        contextStore.Verify(s => s.SaveAsync(
            It.Is<ContextSnapshot>(snapshot =>
                snapshot.SessionId == sessionId
                && snapshot.Metadata.ContainsKey("current_step")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SessionController CreateController(
        ISessionRepository sessionRepository,
        IMessageRepository? messageRepository = null,
        IAgentRunRepository? agentRunRepository = null,
        IDiagnosticDataRepository? diagnosticDataRepository = null,
        ITodoService? todoService = null,
        IContextStore? contextStore = null,
        ITokenEstimator? tokenEstimator = null,
        ContextManagerOptions? contextOptions = null,
        IExecutionTracker? executionTracker = null,
        IAuditService? auditService = null,
        IAgent? agent = null)
    {
        return new SessionController(
            sessionRepository,
            messageRepository ?? Mock.Of<IMessageRepository>(),
            agentRunRepository ?? Mock.Of<IAgentRunRepository>(),
            diagnosticDataRepository ?? Mock.Of<IDiagnosticDataRepository>(),
            todoService ?? Mock.Of<ITodoService>(),
            contextStore ?? Mock.Of<IContextStore>(),
            tokenEstimator ?? new SimpleTokenEstimator(),
            contextOptions ?? new ContextManagerOptions(),
            executionTracker ?? Mock.Of<IExecutionTracker>(),
            Mock.Of<ICheckpointService>(),
            Mock.Of<IInterventionService>(),
            Mock.Of<ISessionRecoveryService>(),
            auditService ?? Mock.Of<IAuditService>(),
            Mock.Of<ISessionStreamPublisher>(),
            agent ?? Mock.Of<IAgent>(),
            Mock.Of<ILogger<SessionController>>());
    }
}