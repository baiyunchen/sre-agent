namespace SreAgent.Repository.Entities;

public class LlmSettingsEntity
{
    public int Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? ModelsJson { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
