using FluentAssertions;
using Microsoft.Extensions.AI;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;
using Xunit;

namespace SreAgent.Framework.Tests.Contexts;

public class DefaultContextManagerTests
{
    private readonly ITokenEstimator _tokenEstimator = new SimpleTokenEstimator();

    private DefaultContextManager CreateContextManager()
    {
        return new DefaultContextManager(_tokenEstimator);
    }

    #region GetChatMessages Tests

    [Fact]
    public void GetChatMessages_WithMultipleMessages_ShouldConvertAll()
    {
        // Arrange
        var contextManager = CreateContextManager();

        contextManager.SetSystemMessage("You are a helpful assistant.");
        contextManager.AddMessage(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Text = "Hello" }]
        });
        contextManager.AddMessage(new Message
        {
            Role = MessageRole.Assistant,
            Parts = [new TextPart { Text = "Hi there!" }]
        });

        // Act
        var result = contextManager.GetChatMessages();

        // Assert
        result.Should().HaveCount(3);
        result[0].Role.Should().Be(ChatRole.System);
        result[1].Role.Should().Be(ChatRole.User);
        result[2].Role.Should().Be(ChatRole.Assistant);
    }

    #endregion

    #region AddUserMessage Tests

    [Fact]
    public void AddUserMessage_ShouldAddMessageWithUserRole()
    {
        // Arrange
        var contextManager = CreateContextManager();

        // Act
        contextManager.AddUserMessage("Hello, world!");

        // Assert
        var messages = contextManager.GetMessages();
        messages.Should().HaveCount(1);
        messages[0].Role.Should().Be(MessageRole.User);
        messages[0].Parts.Should().HaveCount(1);
        ((TextPart)messages[0].Parts[0]).Text.Should().Be("Hello, world!");
    }

    #endregion

    #region AddAssistantMessage Tests

    [Fact]
    public void AddAssistantMessage_ShouldAddMessageWithAgentId()
    {
        // Arrange
        var contextManager = CreateContextManager();
        var chatMessage = new ChatMessage(ChatRole.Assistant, "I can help you with that.");

        // Act
        contextManager.AddAssistantMessage(chatMessage, "agent-001");

        // Assert
        var messages = contextManager.GetMessages();
        messages.Should().HaveCount(1);
        messages[0].Role.Should().Be(MessageRole.Assistant);
        messages[0].Metadata.AgentId.Should().Be("agent-001");
    }

    [Fact]
    public void AddAssistantMessage_WithFunctionCall_ShouldConvertCorrectly()
    {
        // Arrange
        var contextManager = CreateContextManager();
        var functionCall = new FunctionCallContent("call_123", "test_tool",
            new Dictionary<string, object?> { { "param1", "value1" } });
        var chatMessage = new ChatMessage(ChatRole.Assistant, [functionCall]);

        // Act
        contextManager.AddAssistantMessage(chatMessage);

        // Assert
        var messages = contextManager.GetMessages();
        messages.Should().HaveCount(1);
        messages[0].Parts[0].Should().BeOfType<ToolCallPart>();
        var toolCallPart = (ToolCallPart)messages[0].Parts[0];
        toolCallPart.ToolCallId.Should().Be("call_123");
        toolCallPart.Name.Should().Be("test_tool");
    }

    #endregion

    #region AddToolResultMessage Tests

    [Fact]
    public void AddToolResultMessage_WithSingleResult_ShouldAddCorrectly()
    {
        // Arrange
        var contextManager = CreateContextManager();
        var toolResults = new List<(string CallId, string ToolName, ToolResult Result)>
        {
            ("call_123", "test_tool", ToolResult.Success("Operation completed"))
        };

        // Act
        contextManager.AddToolResultMessage(toolResults);

        // Assert
        var messages = contextManager.GetMessages();
        messages.Should().HaveCount(1);
        messages[0].Role.Should().Be(MessageRole.Tool);
        messages[0].Parts.Should().HaveCount(1);
        var toolResultPart = (ToolResultPart)messages[0].Parts[0];
        toolResultPart.ToolCallId.Should().Be("call_123");
        toolResultPart.ToolName.Should().Be("test_tool");
        toolResultPart.IsSuccess.Should().BeTrue();
        toolResultPart.Content.Should().Be("Operation completed");
    }

    [Fact]
    public void AddToolResultMessage_WithMultipleResults_ShouldAddAllResults()
    {
        // Arrange
        var contextManager = CreateContextManager();
        var toolResults = new List<(string CallId, string ToolName, ToolResult Result)>
        {
            ("call_1", "tool_a", ToolResult.Success("Result A")),
            ("call_2", "tool_b", ToolResult.Failure("Error B")),
            ("call_3", "tool_c", ToolResult.Success("Result C"))
        };

        // Act
        contextManager.AddToolResultMessage(toolResults);

        // Assert
        var messages = contextManager.GetMessages();
        messages.Should().HaveCount(1);
        messages[0].Parts.Should().HaveCount(3);
    }

    #endregion

    #region AddHistoryMessages Tests

    [Fact]
    public void AddHistoryMessages_ShouldAddAllMessages()
    {
        // Arrange
        var contextManager = CreateContextManager();
        var historyMessages = new List<ChatMessage>
        {
            new(ChatRole.User, "First message"),
            new(ChatRole.Assistant, "First response"),
            new(ChatRole.User, "Second message")
        };

        // Act
        contextManager.AddHistoryMessages(historyMessages);

        // Assert
        var messages = contextManager.GetMessages();
        messages.Should().HaveCount(3);
        messages[0].Role.Should().Be(MessageRole.User);
        messages[1].Role.Should().Be(MessageRole.Assistant);
        messages[2].Role.Should().Be(MessageRole.User);
    }

    #endregion

    #region GetChatMessages with Custom Builder Tests

    [Fact]
    public void GetChatMessages_WithCustomBuilder_ShouldUseBuilder()
    {
        // Arrange
        var contextManager = CreateContextManager();
        contextManager.AddUserMessage("Hello");
        var customBuilder = new TestChatMessageBuilder();

        // Act
        var result = contextManager.GetChatMessages(customBuilder);

        // Assert
        customBuilder.BuildWasCalled.Should().BeTrue();
        customBuilder.ReceivedMessages.Should().HaveCount(1);
    }

    private class TestChatMessageBuilder : IChatMessageBuilder
    {
        public bool BuildWasCalled { get; private set; }
        public IReadOnlyList<Message>? ReceivedMessages { get; private set; }

        public IReadOnlyList<ChatMessage> Build(IReadOnlyList<Message> messages)
        {
            BuildWasCalled = true;
            ReceivedMessages = messages;
            return DefaultChatMessageBuilder.Instance.Build(messages);
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullConversationFlow_ShouldWorkCorrectly()
    {
        // Arrange
        var contextManager = CreateContextManager();

        // Act - Simulate a full conversation
        contextManager.SetSystemMessage("You are a helpful assistant.");
        contextManager.AddUserMessage("What's the weather?");

        // Simulate assistant response with tool call
        var assistantResponse = new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_1", "get_weather", new Dictionary<string, object?> { { "city", "Beijing" } })]);
        contextManager.AddAssistantMessage(assistantResponse, "sre-agent");

        // Simulate tool result
        contextManager.AddToolResultMessage(new List<(string, string, ToolResult)>
        {
            ("call_1", "get_weather", ToolResult.Success("Sunny, 25°C"))
        });

        // Final assistant response
        var finalResponse = new ChatMessage(ChatRole.Assistant, "The weather in Beijing is sunny with 25°C.");
        contextManager.AddAssistantMessage(finalResponse, "sre-agent");

        // Assert
        var chatMessages = contextManager.GetChatMessages();
        chatMessages.Should().HaveCount(5);
        chatMessages[0].Role.Should().Be(ChatRole.System);
        chatMessages[1].Role.Should().Be(ChatRole.User);
        chatMessages[2].Role.Should().Be(ChatRole.Assistant);
        chatMessages[3].Role.Should().Be(ChatRole.Tool);
        chatMessages[4].Role.Should().Be(ChatRole.Assistant);

        // Verify the last message content
        var lastContent = chatMessages[4].Contents.OfType<TextContent>().FirstOrDefault();
        lastContent.Should().NotBeNull();
        lastContent!.Text.Should().Contain("Beijing");
    }

    #endregion
}
