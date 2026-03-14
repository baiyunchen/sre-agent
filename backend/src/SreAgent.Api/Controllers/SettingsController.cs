using Microsoft.AspNetCore.Mvc;
using SreAgent.Application.Services;

namespace SreAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ILlmSettingsService _llmSettingsService;

    public SettingsController(ILlmSettingsService llmSettingsService)
    {
        _llmSettingsService = llmSettingsService;
    }

    [HttpGet("/api/settings/llm")]
    public async Task<IActionResult> GetLlmConfig(CancellationToken ct = default)
    {
        var config = await _llmSettingsService.GetCurrentAsync(ct);
        return Ok(config);
    }

    [HttpPut("/api/settings/llm")]
    public async Task<IActionResult> UpdateLlmConfig([FromBody] LlmConfigUpdateRequest? request, CancellationToken ct = default)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        try
        {
            var updated = await _llmSettingsService.UpdateAsync(new LlmConfigUpdateInput
            {
                Provider = request.Provider,
                ApiKey = request.ApiKey,
                Models = request.Models,
            }, ct);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("/api/settings/llm/providers")]
    public async Task<IActionResult> GetAvailableProviders(CancellationToken ct = default)
    {
        var providers = await _llmSettingsService.GetProvidersAsync(ct);
        return Ok(providers);
    }
}

public record LlmConfigUpdateRequest
{
    public string Provider { get; init; } = "";
    public string? ApiKey { get; init; }
    public Dictionary<string, string>? Models { get; init; }
}
