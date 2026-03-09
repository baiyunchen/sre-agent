using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SreAgent.Framework.Contexts;
using SreAgent.Repository;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Serialization;

namespace SreAgent.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL-backed implementation of IContextStore.
/// Stores context snapshots by converting Framework Messages to MessageEntities,
/// and reconstructs them on load.
/// </summary>
public class PostgresContextStore : IContextStore
{
    private readonly AppDbContext _context;

    private static readonly JsonSerializerOptions JsonOptions = MessagePartJsonConverter.DefaultOptions;

    public PostgresContextStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(ContextSnapshot snapshot, CancellationToken ct = default)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == snapshot.SessionId, ct);
        if (session == null)
        {
            session = new SessionEntity
            {
                Id = snapshot.SessionId,
                Status = "Running",
                CreatedAt = snapshot.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Sessions.Add(session);
        }
        else
        {
            session.UpdatedAt = DateTime.UtcNow;
        }

        // Remove existing messages for this session and replace with snapshot
        var existingMessages = await _context.Messages
            .Where(m => m.SessionId == snapshot.SessionId)
            .ToListAsync(ct);
        _context.Messages.RemoveRange(existingMessages);

        // Save all messages from the snapshot
        foreach (var message in snapshot.Messages)
        {
            _context.Messages.Add(ToEntity(message, snapshot.SessionId));
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<ContextSnapshot?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null) return null;

        var messageEntities = await _context.Messages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var messages = messageEntities.Select(ToFrameworkMessage).ToList();

        // Separate system message
        string? systemMessage = null;
        var nonSystemMessages = new List<Message>();
        foreach (var msg in messages)
        {
            if (msg.Role == MessageRole.System)
                systemMessage = msg.Parts.OfType<TextPart>().FirstOrDefault()?.Text;
            else
                nonSystemMessages.Add(msg);
        }

        return new ContextSnapshot
        {
            SessionId = sessionId,
            SystemMessage = systemMessage,
            Messages = nonSystemMessages,
            CreatedAt = session.CreatedAt,
            LastUpdatedAt = session.UpdatedAt,
            EstimatedTokenCount = messageEntities.Sum(m => m.EstimatedTokens)
        };
    }

    public async Task DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        var messages = await _context.Messages
            .Where(m => m.SessionId == sessionId)
            .ToListAsync(ct);
        _context.Messages.RemoveRange(messages);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.Sessions.AnyAsync(s => s.Id == sessionId, ct);
    }

    private static MessageEntity ToEntity(Message message, Guid sessionId)
    {
        return new MessageEntity
        {
            Id = message.Id,
            SessionId = sessionId,
            Role = message.Role.ToString(),
            Parts = JsonSerializer.SerializeToDocument(message.Parts, JsonOptions),
            Metadata = JsonSerializer.SerializeToDocument(message.Metadata, JsonOptions),
            EstimatedTokens = message.Metadata.EstimatedTokens,
            AgentId = message.Metadata.AgentId,
            CreatedAt = message.CreatedAt
        };
    }

    private static Message ToFrameworkMessage(MessageEntity entity)
    {
        var parts = JsonSerializer.Deserialize<List<MessagePart>>(entity.Parts, JsonOptions) ?? [];
        var metadata = entity.Metadata != null
            ? JsonSerializer.Deserialize<MessageMetadata>(entity.Metadata, JsonOptions) ?? new MessageMetadata()
            : new MessageMetadata();

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

