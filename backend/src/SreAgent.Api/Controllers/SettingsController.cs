using Microsoft.AspNetCore.Mvc;
using SreAgent.Framework.Providers;

namespace SreAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IModelProviderAccessor _providerAccessor;

    public SettingsController(IModelProviderAccessor providerAccessor)
    {
        _providerAccessor = providerAccessor;
    }

    [HttpGet("/api/settings/llm")]
    public IActionResult GetLlmConfig()
    {
        var provider = _providerAccessor.Current;
        var options = provider.Options;

        return Ok(BuildLlmConfigResponse(options));
    }

    [HttpPut("/api/settings/llm")]
    public IActionResult UpdateLlmConfig([FromBody] LlmConfigUpdateRequest? request)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        if (string.IsNullOrWhiteSpace(request.Provider))
            return BadRequest(new { error = "provider is required" });

        var knownOptions = GetWellKnownProvider(request.Provider);
        if (knownOptions == null)
            return BadRequest(new { error = $"Unknown provider: {request.Provider}. Available: AliyunBailian, Zhipu" });

        ModelProviderOptions finalOptions;
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            finalOptions = new ModelProviderOptions
            {
                Name = knownOptions.Name,
                BaseUrl = knownOptions.BaseUrl,
                ApiKey = request.ApiKey,
                ApiKeyEnvironmentVariable = knownOptions.ApiKeyEnvironmentVariable,
                Models = knownOptions.Models,
                Pricing = knownOptions.Pricing,
                TokenLimits = knownOptions.TokenLimits,
            };
        }
        else
        {
            finalOptions = knownOptions;
        }

        _providerAccessor.Update(finalOptions);

        return Ok(BuildLlmConfigResponse(finalOptions));
    }

    [HttpGet("/api/settings/llm/providers")]
    public IActionResult GetAvailableProviders()
    {
        var providers = new[]
        {
            BuildProviderInfo(WellKnownModelProviders.AliyunBailian, "Aliyun Bailian (通义千问)"),
            BuildProviderInfo(WellKnownModelProviders.Zhipu, "Zhipu AI (智谱清言)"),
        };

        return Ok(new { providers });
    }

    private static object BuildLlmConfigResponse(ModelProviderOptions options)
    {
        bool apiKeyConfigured;
        string? apiKeyHint = null;

        try
        {
            var key = options.GetApiKey();
            apiKeyConfigured = true;
            apiKeyHint = MaskApiKey(key);
        }
        catch
        {
            apiKeyConfigured = !string.IsNullOrEmpty(options.ApiKey);
            if (apiKeyConfigured)
                apiKeyHint = MaskApiKey(options.ApiKey!);
            else if (!string.IsNullOrEmpty(options.ApiKeyEnvironmentVariable))
                apiKeyHint = $"Env: {options.ApiKeyEnvironmentVariable} (not set)";
        }

        var models = new Dictionary<string, string>();
        foreach (var cap in Enum.GetValues<ModelCapability>())
        {
            try
            {
                models[cap.ToString()] = options.GetModel(cap);
            }
            catch (InvalidOperationException)
            {
                // capability not configured for this provider
            }
        }

        return new
        {
            provider = options.Name,
            baseUrl = options.BaseUrl,
            apiKeyConfigured,
            apiKeyHint,
            models,
        };
    }

    private static object BuildProviderInfo(ModelProviderOptions options, string displayName)
    {
        var models = new Dictionary<string, string>();
        foreach (var cap in Enum.GetValues<ModelCapability>())
        {
            try
            {
                models[cap.ToString()] = options.GetModel(cap);
            }
            catch (InvalidOperationException)
            {
                // capability not configured for this provider
            }
        }

        return new
        {
            name = options.Name,
            displayName,
            baseUrl = options.BaseUrl,
            models,
        };
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

    private static string MaskApiKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 3)
            return "***";
        if (key.Length <= 8)
            return "***" + key[^3..];
        return key[..3] + "***" + key[^4..];
    }
}

public record LlmConfigUpdateRequest
{
    public string Provider { get; init; } = "";
    public string? ApiKey { get; init; }
}
