using FluentAssertions;
using Moq;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Contexts;
using Xunit;

namespace SreAgent.Framework.Tests.Agents;

public class TokenManagerTests
{
    private readonly TokenManager _tokenManager;
    private readonly SimpleTokenEstimator _tokenEstimator;

    public TokenManagerTests()
    {
        _tokenEstimator = new SimpleTokenEstimator();
        _tokenManager = new TokenManager(_tokenEstimator);
    }

    #region EstimateToolDefinitionTokens Tests

    [Fact]
    public void EstimateToolDefinitionTokens_WithEmptyTools_ShouldReturnZero()
    {
        // Arrange
        var tools = new List<ITool>();

        // Act
        var result = _tokenManager.EstimateToolDefinitionTokens(tools);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void EstimateToolDefinitionTokens_WithTools_ShouldEstimateCorrectly()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.GetDetail()).Returns(new ToolDetail
        {
            Name = "test_tool",
            Description = "A test tool for testing purposes",
            ParameterSchema = """{"type":"object","properties":{"param1":{"type":"string"}}}"""
        });
        var tools = new List<ITool> { mockTool.Object };

        // Act
        var result = _tokenManager.EstimateToolDefinitionTokens(tools);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EstimateToolDefinitionTokens_WithMultipleTools_ShouldSumTokens()
    {
        // Arrange
        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.GetDetail()).Returns(new ToolDetail
        {
            Name = "tool1",
            Description = "First tool",
            ParameterSchema = "{}"
        });

        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.GetDetail()).Returns(new ToolDetail
        {
            Name = "tool2",
            Description = "Second tool with longer description",
            ParameterSchema = """{"type":"object"}"""
        });

        var tools = new List<ITool> { mockTool1.Object, mockTool2.Object };

        // Act
        var result = _tokenManager.EstimateToolDefinitionTokens(tools);

        // Assert
        var singleToolTokens = _tokenManager.EstimateToolDefinitionTokens(new List<ITool> { mockTool1.Object });
        result.Should().BeGreaterThan(singleToolTokens);
    }

    #endregion

    #region NeedsTrimming Tests

    [Fact]
    public void NeedsTrimming_WhenUnderLimit_ShouldReturnFalse()
    {
        // Arrange
        var currentTokens = 1000;
        var effectiveLimit = 2000;

        // Act
        var result = _tokenManager.NeedsTrimming(currentTokens, effectiveLimit);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void NeedsTrimming_WhenOverLimit_ShouldReturnTrue()
    {
        // Arrange
        var currentTokens = 2500;
        var effectiveLimit = 2000;

        // Act
        var result = _tokenManager.NeedsTrimming(currentTokens, effectiveLimit);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void NeedsTrimming_WhenExactlyAtLimit_ShouldReturnFalse()
    {
        // Arrange
        var currentTokens = 2000;
        var effectiveLimit = 2000;

        // Act
        var result = _tokenManager.NeedsTrimming(currentTokens, effectiveLimit);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CalculateTotalTokens Tests

    [Fact]
    public void CalculateTotalTokens_ShouldCombineContextAndToolTokens()
    {
        // Arrange
        var contextManager = new DefaultContextManager(_tokenEstimator);
        contextManager.AddMessage(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Text = "This is a test message" }]
        });

        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.GetDetail()).Returns(new ToolDetail
        {
            Name = "test",
            Description = "test",
            ParameterSchema = "{}"
        });
        var tools = new List<ITool> { mockTool.Object };

        // Act
        var result = _tokenManager.CalculateTotalTokens(contextManager, tools);

        // Assert
        var contextTokens = contextManager.EstimatedTokenCount;
        var toolTokens = _tokenManager.EstimateToolDefinitionTokens(tools);
        result.Should().Be(contextTokens + toolTokens);
    }

    #endregion

    #region TryTrim Tests

    [Fact]
    public void TryTrim_ShouldDelegateToTrimmer()
    {
        // Arrange
        var contextManager = new DefaultContextManager(_tokenEstimator);
        contextManager.AddMessage(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Text = "Message 1" }]
        });

        var mockTrimmer = new Mock<IContextTrimmer>();
        mockTrimmer.Setup(t => t.Trim(It.IsAny<IContextManager>(), It.IsAny<int>()))
            .Returns(TrimResult.Success(100, 80, 1, "MockTrimmer"));

        var tools = new List<ITool>();

        // Act
        var result = _tokenManager.TryTrim(
            contextManager,
            mockTrimmer.Object,
            1000,
            0.8,
            tools);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mockTrimmer.Verify(t => t.Trim(contextManager, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void TryTrim_WhenTrimmerFails_ShouldReturnFailure()
    {
        // Arrange
        var contextManager = new DefaultContextManager(_tokenEstimator);

        var mockTrimmer = new Mock<IContextTrimmer>();
        mockTrimmer.Setup(t => t.Trim(It.IsAny<IContextManager>(), It.IsAny<int>()))
            .Returns(TrimResult.Failure("Cannot trim further"));

        var tools = new List<ITool>();

        // Act
        var result = _tokenManager.TryTrim(
            contextManager,
            mockTrimmer.Object,
            1000,
            0.8,
            tools);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Cannot trim further");
    }

    #endregion
}
