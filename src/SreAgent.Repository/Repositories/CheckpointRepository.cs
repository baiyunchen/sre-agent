using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface ICheckpointRepository
{
    Task<CheckpointEntity> CreateAsync(CheckpointEntity checkpoint, CancellationToken ct = default);
    Task<CheckpointEntity?> GetLatestAsync(Guid sessionId, CancellationToken ct = default);
    Task<CheckpointEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CheckpointEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task DeleteOldCheckpointsAsync(Guid sessionId, int keepCount, CancellationToken ct = default);
}

public class CheckpointRepository : ICheckpointRepository
{
    private readonly AppDbContext _context;

    public CheckpointRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CheckpointEntity> CreateAsync(CheckpointEntity checkpoint, CancellationToken ct = default)
    {
        _context.Checkpoints.Add(checkpoint);
        await _context.SaveChangesAsync(ct);
        return checkpoint;
    }

    public async Task<CheckpointEntity?> GetLatestAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.Checkpoints
            .Where(c => c.SessionId == sessionId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CheckpointEntity?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Checkpoints.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<CheckpointEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.Checkpoints
            .Where(c => c.SessionId == sessionId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task DeleteOldCheckpointsAsync(Guid sessionId, int keepCount, CancellationToken ct = default)
    {
        var toDelete = await _context.Checkpoints
            .Where(c => c.SessionId == sessionId)
            .OrderByDescending(c => c.CreatedAt)
            .Skip(keepCount)
            .ToListAsync(ct);

        if (toDelete.Count > 0)
        {
            _context.Checkpoints.RemoveRange(toDelete);
            await _context.SaveChangesAsync(ct);
        }
    }
}
