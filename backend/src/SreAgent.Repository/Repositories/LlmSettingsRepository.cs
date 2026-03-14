using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface ILlmSettingsRepository
{
    Task<LlmSettingsEntity?> GetAsync(CancellationToken ct = default);
    Task UpsertAsync(LlmSettingsEntity settings, CancellationToken ct = default);
}

public class LlmSettingsRepository : ILlmSettingsRepository
{
    private const int SingletonId = 1;
    private readonly AppDbContext _context;

    public LlmSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LlmSettingsEntity?> GetAsync(CancellationToken ct = default)
    {
        return await _context.LlmSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == SingletonId, ct);
    }

    public async Task UpsertAsync(LlmSettingsEntity settings, CancellationToken ct = default)
    {
        var existing = await _context.LlmSettings.FirstOrDefaultAsync(x => x.Id == SingletonId, ct);
        if (existing == null)
        {
            settings.Id = SingletonId;
            settings.UpdatedAt = DateTime.UtcNow;
            _context.LlmSettings.Add(settings);
        }
        else
        {
            existing.ProviderName = settings.ProviderName;
            existing.ApiKey = settings.ApiKey;
            existing.ModelsJson = settings.ModelsJson;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }
}
