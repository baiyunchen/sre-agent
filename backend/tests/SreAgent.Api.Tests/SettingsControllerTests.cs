using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SreAgent.Api.Controllers;
using SreAgent.Api.Services;
using SreAgent.Application.Services;
using SreAgent.Framework.Providers;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;
using Xunit;

namespace SreAgent.Api.Tests;

public class SettingsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static (SettingsController Controller, ModelProviderAccessor Accessor) CreateController(ModelProviderOptions? initialOptions = null)
    {
        var accessor = new ModelProviderAccessor(initialOptions ?? WellKnownModelProviders.AliyunBailian);
        var repository = new InMemoryLlmSettingsRepository();
        var service = new LlmSettingsService(accessor, repository, NullLogger<LlmSettingsService>.Instance);
        return (new SettingsController(service), accessor);
    }

    private sealed class InMemoryLlmSettingsRepository : ILlmSettingsRepository
    {
        private LlmSettingsEntity? _settings;

        public Task<LlmSettingsEntity?> GetAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_settings);
        }

        public Task UpsertAsync(LlmSettingsEntity settings, CancellationToken ct = default)
        {
            _settings = new LlmSettingsEntity
            {
                Id = settings.Id,
                ProviderName = settings.ProviderName,
                ApiKey = settings.ApiKey,
                ModelsJson = settings.ModelsJson,
                UpdatedAt = settings.UpdatedAt,
            };
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetLlmConfig_ShouldReturnCurrentProviderConfig()
    {
        var (controller, _) = CreateController();
        var result = await controller.GetLlmConfig();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("provider").GetString().Should().Be("AliyunBailian");
        root.GetProperty("baseUrl").GetString().Should().Contain("dashscope.aliyuncs.com");
        root.GetProperty("models").GetProperty("Large").GetString().Should().Be("qwen3.5-plus");
        root.GetProperty("models").GetProperty("Small").GetString().Should().Be("qwen-turbo");
    }

    [Fact]
    public async Task GetLlmConfig_WithZhipu_ShouldReturnZhipuConfig()
    {
        var (controller, _) = CreateController(WellKnownModelProviders.Zhipu);
        var result = await controller.GetLlmConfig();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("provider").GetString().Should().Be("Zhipu");
        root.GetProperty("baseUrl").GetString().Should().Contain("bigmodel.cn");
        root.GetProperty("models").GetProperty("Large").GetString().Should().Be("glm-4.6");
    }

    [Fact]
    public async Task UpdateLlmConfig_SwitchToZhipuWithApiKey_ShouldReturnZhipuConfig()
    {
        var (controller, _) = CreateController();
        var request = new LlmConfigUpdateRequest
        {
            Provider = "Zhipu",
            ApiKey = "zhipu-test-key-123456"
        };
        var result = await controller.UpdateLlmConfig(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("provider").GetString().Should().Be("Zhipu");
        doc.RootElement.GetProperty("models").GetProperty("Large").GetString().Should().Be("glm-4.6");
    }

    [Fact]
    public async Task UpdateLlmConfig_SwitchProviderWithoutApiKey_ShouldReturn400()
    {
        var (controller, _) = CreateController();
        var result = await controller.UpdateLlmConfig(new LlmConfigUpdateRequest { Provider = "Zhipu" });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonSerializer.Serialize(badRequest.Value, JsonOptions);
        json.Should().Contain("requires apiKey");
    }

    [Fact]
    public async Task UpdateLlmConfig_SameProviderWithoutApiKey_ShouldKeepExistingApiKey()
    {
        var (controller, _) = CreateController();
        await controller.UpdateLlmConfig(new LlmConfigUpdateRequest
        {
            Provider = "AliyunBailian",
            ApiKey = "sk-keep-existing-123456"
        });

        var updateResult = await controller.UpdateLlmConfig(new LlmConfigUpdateRequest
        {
            Provider = "AliyunBailian",
            Models = new Dictionary<string, string>
            {
                ["Large"] = "qwen-turbo"
            }
        });

        var okResult = updateResult.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiKeyConfigured").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("apiKeyHint").GetString().Should().Contain("***");
    }

    [Fact]
    public async Task UpdateLlmConfig_UnknownCapabilityInModels_ShouldReturn400()
    {
        var (controller, _) = CreateController();
        var result = await controller.UpdateLlmConfig(new LlmConfigUpdateRequest
        {
            Provider = "AliyunBailian",
            ApiKey = "sk-valid-123456",
            Models = new Dictionary<string, string>
            {
                ["UnknownCapability"] = "qwen-turbo"
            }
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonSerializer.Serialize(badRequest.Value, JsonOptions);
        json.Should().Contain("Unknown model capability");
    }

    [Fact]
    public async Task UpdateLlmConfig_UnsupportedModelInModels_ShouldReturn400()
    {
        var (controller, _) = CreateController();
        var result = await controller.UpdateLlmConfig(new LlmConfigUpdateRequest
        {
            Provider = "AliyunBailian",
            ApiKey = "sk-valid-123456",
            Models = new Dictionary<string, string>
            {
                ["Large"] = "not-supported-model"
            }
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonSerializer.Serialize(badRequest.Value, JsonOptions);
        json.Should().Contain("Unsupported model");
    }

    [Fact]
    public async Task UpdateLlmConfig_WithApiKey_ShouldShowConfigured()
    {
        var (controller, _) = CreateController();
        var request = new LlmConfigUpdateRequest
        {
            Provider = "AliyunBailian",
            ApiKey = "sk-test1234567890abcdef"
        };
        var result = await controller.UpdateLlmConfig(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiKeyConfigured").GetBoolean().Should().BeTrue();
        var hint = doc.RootElement.GetProperty("apiKeyHint").GetString()!;
        hint.Should().NotBe("sk-test1234567890abcdef");
        hint.Should().Contain("***");
    }

    [Fact]
    public async Task UpdateLlmConfig_UnknownProvider_ShouldReturn400()
    {
        var (controller, _) = CreateController();
        var request = new LlmConfigUpdateRequest { Provider = "UnknownProvider" };
        var result = await controller.UpdateLlmConfig(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateLlmConfig_EmptyProvider_ShouldReturn400()
    {
        var (controller, _) = CreateController();
        var request = new LlmConfigUpdateRequest { Provider = "" };
        var result = await controller.UpdateLlmConfig(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAvailableProviders_ShouldReturnKnownProviders()
    {
        var (controller, _) = CreateController();
        var result = await controller.GetAvailableProviders();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var providers = doc.RootElement.GetProperty("providers");

        providers.GetArrayLength().Should().Be(2);

        var aliyun = providers[0];
        aliyun.GetProperty("name").GetString().Should().Be("AliyunBailian");
        aliyun.GetProperty("displayName").GetString().Should().Contain("通义千问");

        var zhipu = providers[1];
        zhipu.GetProperty("name").GetString().Should().Be("Zhipu");
        zhipu.GetProperty("displayName").GetString().Should().Contain("智谱清言");
        zhipu.GetProperty("availableModels").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateLlmConfig_ShouldPersistAcrossGets()
    {
        var (controller, _) = CreateController();

        await controller.UpdateLlmConfig(new LlmConfigUpdateRequest
        {
            Provider = "Zhipu",
            ApiKey = "zhipu-persist-key-123456"
        });

        var getResult = await controller.GetLlmConfig();
        var okResult = getResult.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("provider").GetString().Should().Be("Zhipu");
    }

    [Fact]
    public void ModelProviderAccessor_Update_ShouldReflectInCurrent()
    {
        var accessor = new ModelProviderAccessor(WellKnownModelProviders.AliyunBailian);
        accessor.Current.Name.Should().Be("AliyunBailian");

        accessor.Update(WellKnownModelProviders.Zhipu);
        accessor.Current.Name.Should().Be("Zhipu");
    }

    [Fact]
    public async Task UpdateLlmConfig_WithShortApiKey_ShouldNotExposeFullKey()
    {
        var (controller, _) = CreateController();
        var request = new LlmConfigUpdateRequest
        {
            Provider = "AliyunBailian",
            ApiKey = "ab"
        };
        var result = await controller.UpdateLlmConfig(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);

        var hint = doc.RootElement.GetProperty("apiKeyHint").GetString()!;
        hint.Should().Be("***");
        hint.Should().NotContain("ab");
    }

    [Fact]
    public async Task UpdateLlmConfig_NullBody_ShouldReturn400()
    {
        var (controller, _) = CreateController();
        var result = await controller.UpdateLlmConfig(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
