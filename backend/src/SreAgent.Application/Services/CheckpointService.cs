using System.Text.Json;
using SreAgent.Framework.Contexts;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using SreAgent.Repository.Serialization;

namespace SreAgent.Application.Services;

public interface ICheckpointService
{
    Task<CheckpointEntity> CreateCheckpointAsync(
        Guid sessionId, IContextManager contextManager, string name, CancellationToken ct = default);
    Task<CheckpointEntity?> GetLatestCheckpointAsync(Guid sessionId, CancellationToken ct = default);
    Task<IContextManager> RestoreFromCheckpointAsync(
        Guid checkpointId, ITokenEstimator tokenEstimator, ContextManagerOptions? options = null, CancellationToken ct = default);
}

public class CheckpointService : ICheckpointService
{
    private readonly ICheckpointRepository _checkpointRepository;
    private readonly IMessageRepository _messageRepository;
    private const int MaxCheckpointsPerSession = 5;

    public CheckpointService(ICheckpointRepository checkpointRepository, IMessageRepository messageRepository)
    {
        _checkpointRepository = checkpointRepository;
        _messageRepository = messageRepository;
    }

    public async Task<CheckpointEntity> CreateCheckpointAsync(
        Guid sessionId, IContextManager contextManager, string name, CancellationToken ct = default)
    {
        var messages = contextManager.GetMessages();
        var systemMessage = contextManager.GetSystemMessage();
        var systemText = systemMessage?.Parts.OfType<TextPart>().FirstOrDefault()?.Text;

        var messageIds = messages
            .Where(m => m.Role != MessageRole.System)
            .Select(m => m.Id)
            .ToList();

        var checkpoint = new CheckpointEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            CheckpointName = name,
            StepNumber = messages.Count,
            SystemMessage = systemText,
            MessageIds = JsonSerializer.SerializeToDocument(messageIds),
            SessionState = JsonSerializer.SerializeToDocument(new
            {
                sessionId,
                estimatedTokenCount = contextManager.EstimatedTokenCount,
                messageCount = messages.Count
            }),
            CreatedAt = DateTime.UtcNow
        };

        await _checkpointRepository.CreateAsync(checkpoint, ct);

        // Clean up old checkpoints
        await _checkpointRepository.DeleteOldCheckpointsAsync(sessionId, MaxCheckpointsPerSession, ct);

        return checkpoint;
    }

    public async Task<CheckpointEntity?> GetLatestCheckpointAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _checkpointRepository.GetLatestAsync(sessionId, ct);
    }

    public async Task<IContextManager> RestoreFromCheckpointAsync(
        Guid checkpointId, ITokenEstimator tokenEstimator, ContextManagerOptions? options = null, CancellationToken ct = default)
    {
        var checkpoint = await _checkpointRepository.GetAsync(checkpointId, ct)
            ?? throw new InvalidOperationException($"Checkpoint {checkpointId} not found");

        // Parse message IDs from checkpoint
        var messageIds = JsonSerializer.Deserialize<List<Guid>>(checkpoint.MessageIds) ?? [];

        // Load messages from DB
        var messageEntities = await _messageRepository.GetByIdsAsync(messageIds, ct);

        // Rebuild context manager
        var context = new DefaultContextManager(
            checkpoint.SessionId, tokenEstimator, options);

        if (!string.IsNullOrEmpty(checkpoint.SystemMessage))
        {
            context.SetSystemMessage(checkpoint.SystemMessage);
        }

        foreach (var entity in messageEntities)
        {
            var message = MessageEntityToFramework(entity);
            context.AddMessage(message);
        }

        return context;
    }

    private static Message MessageEntityToFramework(MessageEntity entity)
    {
        var parts = JsonSerializer.Deserialize<List<MessagePart>>(entity.Parts, MessagePartJsonConverter.DefaultOptions) ?? [];
        var metadata = entity.Metadata != null
            ? JsonSerializer.Deserialize<MessageMetadata>(entity.Metadata, MessagePartJsonConverter.DefaultOptions) ?? new()
            : new();

        return new Message
        {
            Id = entity.Id,
            Role = Enum.Parse<MessageRole>(entity.Role),
            Parts = parts,
            Metadata = metadata,
            CreatedAt = entity.CreatedAt
        };
    }
}
