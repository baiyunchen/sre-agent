using FluentAssertions;
using Microsoft.Extensions.AI;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;
using Xunit;

namespace SreAgent.Framework.Tests.Agents;

public class MessageConverterTests
{
    #region FromChatMessage Tests

    [Fact]
    public void FromChatMessage_WithTextContent_ShouldConvertCorrectly()
    {
        // Arrange
        var chatMessage = new ChatMessage(ChatRole.User, "Hello, world!");

        // Act
        var result = MessageConverter.FromChatMessage(chatMessage);

        // Assert
        result.Role.Should().Be(MessageRole.User);
        result.Parts.Should().HaveCount(1);
        result.Parts[0].Should().BeOfType<TextPart>();
        ((TextPart)result.Parts[0]).Text.Should().Be("Hello, world!");
    }

    [Fact]
    public void FromChatMessage_WithSystemRole_ShouldConvertCorrectly()
    {
        // Arrange
        var chatMessage = new ChatMessage(ChatRole.System, "You are a helpful assistant.");

        // Act
        var result = MessageConverter.FromChatMessage(chatMessage);

        // Assert
        result.Role.Should().Be(MessageRole.System);
    }

    [Fact]
    public void FromChatMessage_WithAssistantRole_ShouldConvertCorrectly()
    {
        // Arrange
        var chatMessage = new ChatMessage(ChatRole.Assistant, "I can help you with that.");

        // Act
        var result = MessageConverter.FromChatMessage(chatMessage);

        // Assert
        result.Role.Should().Be(MessageRole.Assistant);
    }

    [Fact]
    public void FromChatMessage_WithFunctionCall_ShouldConvertCorrectly()
    {
        // Arrange
        var functionCall = new FunctionCallContent("call_123", "test_tool", new Dictionary<string, object?> { { "param1", "value1" } });
        var chatMessage = new ChatMessage(ChatRole.Assistant, [functionCall]);

        // Act
        var result = MessageConverter.FromChatMessage(chatMessage);

        // Assert
        result.Role.Should().Be(MessageRole.Assistant);
        result.Parts.Should().HaveCount(1);
        result.Parts[0].Should().BeOfType<ToolCallPart>();
        var toolCallPart = (ToolCallPart)result.Parts[0];
        toolCallPart.ToolCallId.Should().Be("call_123");
        toolCallPart.Name.Should().Be("test_tool");
        toolCallPart.Arguments.Should().Contain("param1");
    }

    [Fact]
    public void FromChatMessage_WithFunctionResult_ShouldConvertCorrectly()
    {
        // Arrange
        var functionResult = new FunctionResultContent("call_123", "Operation completed successfully");
        var chatMessage = new ChatMessage(ChatRole.Tool, [functionResult]);

        // Act
        var result = MessageConverter.FromChatMessage(chatMessage);

        // Assert
        result.Role.Should().Be(MessageRole.Tool);
        result.Parts.Should().HaveCount(1);
        result.Parts[0].Should().BeOfType<ToolResultPart>();
        var toolResultPart = (ToolResultPart)result.Parts[0];
        toolResultPart.ToolCallId.Should().Be("call_123");
        toolResultPart.Content.Should().Be("Operation completed successfully");
    }

    #endregion

    #region ToChatMessage Tests

    [Fact]
    public void ToChatMessage_WithTextPart_ShouldConvertCorrectly()
    {
        // Arrange
        var message = new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Text = "Hello, world!" }]
        };

        // Act
        var result = MessageConverter.ToChatMessage(message);

        // Assert
        result.Role.Should().Be(ChatRole.User);
        result.Contents.Should().HaveCount(1);
        result.Contents[0].Should().BeOfType<TextContent>();
        ((TextContent)result.Contents[0]).Text.Should().Be("Hello, world!");
    }

    [Fact]
    public void ToChatMessage_WithToolCallPart_ShouldConvertCorrectly()
    {
        // Arrange
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Parts =
            [
                new ToolCallPart
                {
                    ToolCallId = "call_123",
                    Name = "test_tool",
                    Arguments = """{"param1":"value1"}"""
                }
            ]
        };

        // Act
        var result = MessageConverter.ToChatMessage(message);

        // Assert
        result.Role.Should().Be(ChatRole.Assistant);
        result.Contents.Should().HaveCount(1);
        result.Contents[0].Should().BeOfType<FunctionCallContent>();
        var functionCall = (FunctionCallContent)result.Contents[0];
        functionCall.CallId.Should().Be("call_123");
        functionCall.Name.Should().Be("test_tool");
    }

    [Fact]
    public void ToChatMessage_WithToolResultPart_ShouldConvertCorrectly()
    {
        // Arrange
        var message = new Message
        {
            Role = MessageRole.Tool,
            Parts =
            [
                new ToolResultPart
                {
                    ToolCallId = "call_123",
                    ToolName = "test_tool",
                    IsSuccess = true,
                    Content = "Operation completed"
                }
            ]
        };

        // Act
        var result = MessageConverter.ToChatMessage(message);

        // Assert
        result.Role.Should().Be(ChatRole.Tool);
        result.Contents.Should().HaveCount(1);
        result.Contents[0].Should().BeOfType<FunctionResultContent>();
        var functionResult = (FunctionResultContent)result.Contents[0];
        functionResult.CallId.Should().Be("call_123");
        functionResult.Result.Should().Be("Operation completed");
    }

    #endregion

    #region Roundtrip Tests

    [Fact]
    public void Roundtrip_FromChatMessage_ToChatMessage_ShouldPreserveContent()
    {
        // Arrange
        var originalMessage = new ChatMessage(ChatRole.User, "Hello, world!");

        // Act
        var internalMessage = MessageConverter.FromChatMessage(originalMessage);
        var roundtrippedMessage = MessageConverter.ToChatMessage(internalMessage);

        // Assert
        roundtrippedMessage.Role.Should().Be(originalMessage.Role);
        roundtrippedMessage.Contents.Should().HaveCount(1);
        ((TextContent)roundtrippedMessage.Contents[0]).Text.Should().Be("Hello, world!");
    }

    #endregion
}
