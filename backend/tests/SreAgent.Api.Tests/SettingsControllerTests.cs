using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SreAgent.Api.Controllers;
using SreAgent.Api.Services;
using SreAgent.Framework.Providers;
using Xunit;

namespace SreAgent.Api.Tests;

public class SettingsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static SettingsController CreateController(ModelProviderOptions? initialOptions = null)
    {
        var accessor = new ModelProviderAccessor(initialOptions ?? WellKnownModelProviders.AliyunBailian);
        return new SettingsController(accessor);
    }

    [Fact]
    public void GetLlmConfig_ShouldReturnCurrentProviderConfig()
    {
        var controller = CreateController();
        var result = controller.GetLlmConfig();

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
    public void GetLlmConfig_WithZhipu_ShouldReturnZhipuConfig()
    {
        var controller = CreateController(WellKnownModelProviders.Zhipu);
        var result = controller.GetLlmConfig();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("provider").GetString().Should().Be("Zhipu");
        root.GetProperty("baseUrl").GetString().Should().Contain("bigmodel.cn");
        root.GetProperty("models").GetProperty("Large").GetString().Should().Be("glm-4.6");
    }

    [Fact]
    public void UpdateLlmConfig_SwitchToZhipu_ShouldReturnZhipuConfig()
    {
        var controller = CreateController();
        var request = new LlmConfigUpdateRequest { Provider = "Zhipu" };
        var result = controller.UpdateLlmConfig(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("provider").GetString().Should().Be("Zhipu");
        doc.RootElement.GetProperty("models").GetProperty("Large").GetString().Should().Be("glm-4.6");
    }

    [Fact]
    public void UpdateLlmConfig_WithApiKey_ShouldShowConfigured()
    {
        var controller = CreateController();
        var request = new LlmConfigUpdateRequest
        {
            Provider = "AliyunBailian",
            ApiKey = "sk-test1234567890abcdef"
        };
        var result = controller.UpdateLlmConfig(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("apiKeyConfigured").GetBoolean().Should().BeTrue();
        var hint = doc.RootElement.GetProperty("apiKeyHint").GetString()!;
        hint.Should().NotBe("sk-test1234567890abcdef");
        hint.Should().Contain("***");
    }

    [Fact]
    public void UpdateLlmConfig_UnknownProvider_ShouldReturn400()
    {
        var controller = CreateController();
        var request = new LlmConfigUpdateRequest { Provider = "UnknownProvider" };
        var result = controller.UpdateLlmConfig(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void UpdateLlmConfig_EmptyProvider_ShouldReturn400()
    {
        var controller = CreateController();
        var request = new LlmConfigUpdateRequest { Provider = "" };
        var result = controller.UpdateLlmConfig(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void GetAvailableProviders_ShouldReturnKnownProviders()
    {
        var controller = CreateController();
        var result = controller.GetAvailableProviders();

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
    }

    [Fact]
    public void UpdateLlmConfig_ShouldPersistAcrossGets()
    {
        var accessor = new ModelProviderAccessor(WellKnownModelProviders.AliyunBailian);
        var controller = new SettingsController(accessor);

        controller.UpdateLlmConfig(new LlmConfigUpdateRequest { Provider = "Zhipu" });

        var getResult = controller.GetLlmConfig();
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
    public void UpdateLlmConfig_WithShortApiKey_ShouldNotExposeFullKey()
    {
        var controller = CreateController();
        var request = new LlmConfigUpdateRequest
        {
            Provider = "AliyunBailian",
            ApiKey = "ab"
        };
        var result = controller.UpdateLlmConfig(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value, JsonOptions);
        var doc = JsonDocument.Parse(json);

        var hint = doc.RootElement.GetProperty("apiKeyHint").GetString()!;
        hint.Should().Be("***");
        hint.Should().NotContain("ab");
    }

    [Fact]
    public void UpdateLlmConfig_NullBody_ShouldReturn400()
    {
        var controller = CreateController();
        var result = controller.UpdateLlmConfig(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
