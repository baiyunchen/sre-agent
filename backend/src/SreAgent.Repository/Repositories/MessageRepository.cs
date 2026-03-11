using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IMessageRepository
{
    Task<MessageEntity> AddAsync(MessageEntity message, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<MessageEntity> messages, CancellationToken ct = default);
    Task<IReadOnlyList<MessageEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<MessageEntity>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<int> GetTokenCountAsync(Guid sessionId, CancellationToken ct = default);
}

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _context;

    public MessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MessageEntity> AddAsync(MessageEntity message, CancellationToken ct = default)
    {
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(ct);
        return message;
    }

    public async Task AddRangeAsync(IEnumerable<MessageEntity> messages, CancellationToken ct = default)
    {
        _context.Messages.AddRange(messages);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MessageEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.Messages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MessageEntity>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await _context.Messages
            .Where(m => idList.Contains(m.Id))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetTokenCountAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.Messages
            .Where(m => m.SessionId == sessionId)
            .SumAsync(m => m.EstimatedTokens, ct);
    }
}
