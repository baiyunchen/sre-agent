using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;
using Xunit;

namespace SreAgent.Framework.Tests.Agents;

public class ToolExecutorTests
{
    private readonly ToolExecutor _toolExecutor;

    public ToolExecutorTests()
    {
        _toolExecutor = new ToolExecutor();
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithValidTool_ShouldExecuteSuccessfully()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("test_tool");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<ToolExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResult.Success("Execution completed"));

        var tools = new List<ITool> { mockTool.Object };
        var toolCalls = new List<FunctionCallContent>
        {
            new("call_123", "test_tool", new Dictionary<string, object?> { { "param1", "value1" } })
        };

        // Act
        var results = await _toolExecutor.ExecuteAsync(
            Guid.NewGuid(),
            "agent_1",
            toolCalls,
            tools,
            new Dictionary<string, object>());

        // Assert
        results.Should().HaveCount(1);
        results[0].CallId.Should().Be("call_123");
        results[0].ToolName.Should().Be("test_tool");
        results[0].Result.IsSuccess.Should().BeTrue();
        results[0].Result.Content.Should().Be("Execution completed");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentTool_ShouldReturnFailure()
    {
        // Arrange
        var tools = new List<ITool>();
        var toolCalls = new List<FunctionCallContent>
        {
            new("call_123", "non_existent_tool", null)
        };

        // Act
        var results = await _toolExecutor.ExecuteAsync(
            Guid.NewGuid(),
            "agent_1",
            toolCalls,
            tools,
            new Dictionary<string, object>());

        // Assert
        results.Should().HaveCount(1);
        results[0].Result.IsSuccess.Should().BeFalse();
        results[0].Result.ErrorCode.Should().Be("TOOL_NOT_FOUND");
        results[0].Result.Content.Should().Contain("non_existent_tool");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleToolCalls_ShouldExecuteAll()
    {
        // Arrange
        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.Name).Returns("tool_1");
        mockTool1.Setup(t => t.ExecuteAsync(It.IsAny<ToolExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResult.Success("Result 1"));

        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.Name).Returns("tool_2");
        mockTool2.Setup(t => t.ExecuteAsync(It.IsAny<ToolExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResult.Success("Result 2"));

        var tools = new List<ITool> { mockTool1.Object, mockTool2.Object };
        var toolCalls = new List<FunctionCallContent>
        {
            new("call_1", "tool_1", null),
            new("call_2", "tool_2", null)
        };

        // Act
        var results = await _toolExecutor.ExecuteAsync(
            Guid.NewGuid(),
            "agent_1",
            toolCalls,
            tools,
            new Dictionary<string, object>());

        // Assert
        results.Should().HaveCount(2);
        results[0].ToolName.Should().Be("tool_1");
        results[0].Result.Content.Should().Be("Result 1");
        results[1].ToolName.Should().Be("tool_2");
        results[1].Result.Content.Should().Be("Result 2");
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("throwing_tool");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<ToolExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something went wrong"));

        var tools = new List<ITool> { mockTool.Object };
        var toolCalls = new List<FunctionCallContent>
        {
            new("call_123", "throwing_tool", null)
        };

        // Act
        var results = await _toolExecutor.ExecuteAsync(
            Guid.NewGuid(),
            "agent_1",
            toolCalls,
            tools,
            new Dictionary<string, object>());

        // Assert
        results.Should().HaveCount(1);
        results[0].Result.IsSuccess.Should().BeFalse();
        results[0].Result.Content.Should().Contain("Something went wrong");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCorrectContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var agentId = "test_agent";
        var variables = new Dictionary<string, object> { { "key", "value" } };
        ToolExecutionContext? capturedContext = null;

        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("context_tool");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<ToolExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback<ToolExecutionContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(ToolResult.Success("OK"));

        var tools = new List<ITool> { mockTool.Object };
        var toolCalls = new List<FunctionCallContent>
        {
            new("call_123", "context_tool", new Dictionary<string, object?> { { "param", "test" } })
        };

        // Act
        await _toolExecutor.ExecuteAsync(sessionId, agentId, toolCalls, tools, variables);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.SessionId.Should().Be(sessionId);
        capturedContext.AgentId.Should().Be(agentId);
        capturedContext.Variables.Should().ContainKey("key");
        capturedContext.ToolCallId.Should().Be("call_123");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordDuration()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("slow_tool");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<ToolExecutionContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (ToolExecutionContext _, CancellationToken _) =>
            {
                await Task.Delay(50);
                return ToolResult.Success("Done");
            });

        var tools = new List<ITool> { mockTool.Object };
        var toolCalls = new List<FunctionCallContent>
        {
            new("call_123", "slow_tool", null)
        };

        // Act
        var results = await _toolExecutor.ExecuteAsync(
            Guid.NewGuid(),
            "agent_1",
            toolCalls,
            tools,
            new Dictionary<string, object>());

        // Assert
        results[0].Result.Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(40));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullArguments_ShouldHandleGracefully()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("test_tool");
        mockTool.Setup(t => t.ExecuteAsync(It.IsAny<ToolExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResult.Success("OK"));

        var tools = new List<ITool> { mockTool.Object };
        var toolCalls = new List<FunctionCallContent>
        {
            new("call_123", "test_tool", null)
        };

        // Act
        var results = await _toolExecutor.ExecuteAsync(
            Guid.NewGuid(),
            "agent_1",
            toolCalls,
            tools,
            new Dictionary<string, object>());

        // Assert
        results.Should().HaveCount(1);
        results[0].Result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ToAITool Tests

    // Note: ToAITool and ToAITools tests are skipped because ToAITool is an extension method
    // that cannot be mocked directly. These would require integration tests with real tool implementations.

    #endregion
}
