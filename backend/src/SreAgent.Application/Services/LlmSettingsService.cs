using System.Text.Json;
using Microsoft.Extensions.Logging;
using SreAgent.Framework.Providers;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

public interface ILlmSettingsService
{
    Task<LlmConfigDto> GetCurrentAsync(CancellationToken ct = default);
    Task<LlmConfigDto> UpdateAsync(LlmConfigUpdateInput request, CancellationToken ct = default);
    Task<LlmProvidersDto> GetProvidersAsync(CancellationToken ct = default);
    Task InitializeFromPersistenceAsync(CancellationToken ct = default);
}

public sealed class LlmConfigUpdateInput
{
    public string Provider { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
    public Dictionary<string, string>? Models { get; init; }
}

public sealed class LlmConfigDto
{
    public string Provider { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public bool ApiKeyConfigured { get; init; }
    public string? ApiKeyHint { get; init; }
    public Dictionary<string, string> Models { get; init; } = [];
}

public sealed class LlmProviderInfoDto
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public Dictionary<string, string> Models { get; init; } = [];
    public string[] AvailableModels { get; init; } = [];
}

public sealed class LlmProvidersDto
{
    public IReadOnlyList<LlmProviderInfoDto> Providers { get; init; } = [];
}

public sealed class LlmSettingsService : ILlmSettingsService
{
    private readonly IModelProviderAccessor _providerAccessor;
    private readonly ILlmSettingsRepository _repository;
    private readonly ILogger<LlmSettingsService> _logger;

    public LlmSettingsService(
        IModelProviderAccessor providerAccessor,
        ILlmSettingsRepository repository,
        ILogger<LlmSettingsService> logger)
    {
        _providerAccessor = providerAccessor;
        _repository = repository;
        _logger = logger;
    }

    public Task<LlmConfigDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var options = _providerAccessor.Current.Options;
        return Task.FromResult(BuildLlmConfigResponse(options));
    }

    public async Task<LlmConfigDto> UpdateAsync(LlmConfigUpdateInput request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
            throw new ArgumentException("provider is required");

        var knownOptions = GetWellKnownProvider(request.Provider)
            ?? throw new ArgumentException($"Unknown provider: {request.Provider}. Available: AliyunBailian, Zhipu");

        var currentOptions = _providerAccessor.Current.Options;
        var isSameProvider = string.Equals(
            currentOptions.Name,
            knownOptions.Name,
            StringComparison.OrdinalIgnoreCase);

        if (!isSameProvider && string.IsNullOrWhiteSpace(request.ApiKey))
            throw new ArgumentException($"Switching provider to '{knownOptions.Name}' requires apiKey. Please provide a new API key.");

        var mergedModels = BuildMergedModels(knownOptions, request.Models);
        var finalOptions = new ModelProviderOptions
        {
            Name = knownOptions.Name,
            BaseUrl = knownOptions.BaseUrl,
            ApiKey = !string.IsNullOrWhiteSpace(request.ApiKey)
                ? request.ApiKey
                : isSameProvider
                    ? currentOptions.ApiKey
                    : null,
            ApiKeyEnvironmentVariable = knownOptions.ApiKeyEnvironmentVariable,
            Models = mergedModels,
            Pricing = knownOptions.Pricing,
            TokenLimits = knownOptions.TokenLimits,
        };

        if (!HasUsableApiKey(finalOptions))
            throw new ArgumentException(
                $"API key is required for provider '{finalOptions.Name}'. " +
                $"Please provide apiKey or set environment variable '{finalOptions.ApiKeyEnvironmentVariable}'.");

        _providerAccessor.Update(finalOptions);

        var modelsPayload = finalOptions.Models.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
        await _repository.UpsertAsync(new LlmSettingsEntity
        {
            ProviderName = finalOptions.Name,
            ApiKey = finalOptions.ApiKey,
            ModelsJson = JsonSerializer.Serialize(modelsPayload),
        }, ct);

        return BuildLlmConfigResponse(finalOptions);
    }

    public Task<LlmProvidersDto> GetProvidersAsync(CancellationToken ct = default)
    {
        var providers = new[]
        {
            BuildProviderInfo(WellKnownModelProviders.AliyunBailian, "Aliyun Bailian (通义千问)"),
            BuildProviderInfo(WellKnownModelProviders.Zhipu, "Zhipu AI (智谱清言)"),
        };
        return Task.FromResult(new LlmProvidersDto { Providers = providers });
    }

    public async Task InitializeFromPersistenceAsync(CancellationToken ct = default)
    {
        var persisted = await _repository.GetAsync(ct);
        if (persisted == null)
            return;

        var knownOptions = GetWellKnownProvider(persisted.ProviderName);
        if (knownOptions == null)
        {
            _logger.LogWarning("Skip loading persisted llm settings due to unknown provider: {Provider}", persisted.ProviderName);
            return;
        }

        Dictionary<string, string>? modelOverrides = null;
        if (!string.IsNullOrWhiteSpace(persisted.ModelsJson))
        {
            try
            {
                modelOverrides = JsonSerializer.Deserialize<Dictionary<string, string>>(persisted.ModelsJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize persisted llm models json, fallback to provider defaults");
            }
        }

        var mergedModels = BuildMergedModels(knownOptions, modelOverrides, ignoreUnknownCapability: true, ignoreUnsupportedModel: true);
        var restoredOptions = new ModelProviderOptions
        {
            Name = knownOptions.Name,
            BaseUrl = knownOptions.BaseUrl,
            ApiKey = persisted.ApiKey,
            ApiKeyEnvironmentVariable = knownOptions.ApiKeyEnvironmentVariable,
            Models = mergedModels,
            Pricing = knownOptions.Pricing,
            TokenLimits = knownOptions.TokenLimits,
        };

        _providerAccessor.Update(restoredOptions);
        _logger.LogInformation("Loaded persisted llm settings from database for provider: {Provider}", restoredOptions.Name);
    }

    private static Dictionary<ModelCapability, string> BuildMergedModels(
        ModelProviderOptions knownOptions,
        Dictionary<string, string>? requestModels,
        bool ignoreUnknownCapability = false,
        bool ignoreUnsupportedModel = false)
    {
        var mergedModels = new Dictionary<ModelCapability, string>(knownOptions.Models);
        if (requestModels is not { Count: > 0 })
            return mergedModels;

        var allowedModels = GetAllowedModels(knownOptions);
        foreach (var (capabilityRaw, modelName) in requestModels)
        {
            if (!Enum.TryParse<ModelCapability>(capabilityRaw, ignoreCase: true, out var capability))
            {
                if (ignoreUnknownCapability)
                    continue;

                throw new ArgumentException($"Unknown model capability: {capabilityRaw}");
            }

            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException($"Model for capability '{capabilityRaw}' cannot be empty");

            if (!allowedModels.Contains(modelName))
            {
                if (ignoreUnsupportedModel)
                    continue;

                throw new ArgumentException($"Unsupported model '{modelName}' for provider '{knownOptions.Name}'");
            }

            mergedModels[capability] = modelName;
        }

        return mergedModels;
    }

    private static LlmConfigDto BuildLlmConfigResponse(ModelProviderOptions options)
    {
        bool apiKeyConfigured;
        string? apiKeyHint = null;

        try
        {
            var key = options.GetApiKey();
            apiKeyConfigured = true;
            apiKeyHint = MaskApiKey(key);
        }
        catch (InvalidOperationException)
        {
            apiKeyConfigured = !string.IsNullOrEmpty(options.ApiKey);
            if (apiKeyConfigured)
                apiKeyHint = MaskApiKey(options.ApiKey!);
            else if (!string.IsNullOrEmpty(options.ApiKeyEnvironmentVariable))
                apiKeyHint = $"Env: {options.ApiKeyEnvironmentVariable} (not set)";
        }

        var models = new Dictionary<string, string>();
        foreach (var capability in Enum.GetValues<ModelCapability>())
        {
            try
            {
                models[capability.ToString()] = options.GetModel(capability);
            }
            catch (InvalidOperationException)
            {
                // capability not configured for this provider
            }
        }

        return new LlmConfigDto
        {
            Provider = options.Name,
            BaseUrl = options.BaseUrl,
            ApiKeyConfigured = apiKeyConfigured,
            ApiKeyHint = apiKeyHint,
            Models = models,
        };
    }

    private static LlmProviderInfoDto BuildProviderInfo(ModelProviderOptions options, string displayName)
    {
        var models = new Dictionary<string, string>();
        foreach (var capability in Enum.GetValues<ModelCapability>())
        {
            try
            {
                models[capability.ToString()] = options.GetModel(capability);
            }
            catch (InvalidOperationException)
            {
                // capability not configured for this provider
            }
        }

        var availableModels = GetAllowedModels(options);
        return new LlmProviderInfoDto
        {
            Name = options.Name,
            DisplayName = displayName,
            BaseUrl = options.BaseUrl,
            Models = models,
            AvailableModels = availableModels.OrderBy(x => x).ToArray(),
        };
    }

    private static HashSet<string> GetAllowedModels(ModelProviderOptions options)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in options.Models.Values)
            allowed.Add(model);
        foreach (var model in options.Pricing.Keys)
            allowed.Add(model);

        return allowed;
    }

    private static ModelProviderOptions? GetWellKnownProvider(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "aliyunbailian" => WellKnownModelProviders.AliyunBailian,
            "zhipu" => WellKnownModelProviders.Zhipu,
            _ => null,
        };
    }

    private static bool HasUsableApiKey(ModelProviderOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            return true;

        if (string.IsNullOrWhiteSpace(options.ApiKeyEnvironmentVariable))
            return false;

        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(options.ApiKeyEnvironmentVariable));
    }

    private static string MaskApiKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 3)
            return "***";
        if (key.Length <= 8)
            return "***" + key[^3..];
        return key[..3] + "***" + key[^4..];
    }
}
