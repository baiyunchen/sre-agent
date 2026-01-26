namespace SreAgent.Framework.Providers;

/// <summary>
/// 预定义的 Model Provider 配置
/// 每个 Provider 都预设了各能力级别对应的模型和定价信息
/// 价格单位：元/百万 Token
/// </summary>
public static class WellKnownModelProviders
{
    /// <summary>
    /// 阿里云百炼 (DashScope)
    /// Base URL: https://dashscope.aliyuncs.com/compatible-mode/v1
    /// 价格参考: https://help.aliyun.com/zh/model-studio/billing-for-model-studio
    /// </summary>
    public static ModelProviderOptions AliyunBailian => new()
    {
        Name = "AliyunBailian",
        BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
        ApiKeyEnvironmentVariable = "DASHSCOPE_API_KEY",
        Models = new Dictionary<ModelCapability, string>
        {
            [ModelCapability.Large] = "qwen-max",
            [ModelCapability.Medium] = "qwen-plus",
            [ModelCapability.Small] = "qwen-turbo",
            [ModelCapability.Reasoning] = "qwq-plus",
            [ModelCapability.Coding] = "qwen-coder-plus"
        },
        Pricing = new Dictionary<string, ModelPricing>
        {
            ["qwen-max"] = new()
            {
                ModelName = "qwen-max",
                InputPricePerMillion = 20m,
                OutputPricePerMillion = 60m,
                CachedInputPricePerMillion = 2m
            },
            ["qwen-plus"] = new()
            {
                ModelName = "qwen-plus",
                InputPricePerMillion = 0.8m,
                OutputPricePerMillion = 2m,
                CachedInputPricePerMillion = 0.08m
            },
            ["qwen-turbo"] = new()
            {
                ModelName = "qwen-turbo",
                InputPricePerMillion = 0.3m,
                OutputPricePerMillion = 0.6m,
                CachedInputPricePerMillion = 0.03m
            },
            ["qwq-plus"] = new()
            {
                ModelName = "qwq-plus",
                InputPricePerMillion = 0.8m,
                OutputPricePerMillion = 2m,
                CachedInputPricePerMillion = 0.08m
            },
            ["qwen-coder-plus"] = new()
            {
                ModelName = "qwen-coder-plus",
                InputPricePerMillion = 0.8m,
                OutputPricePerMillion = 2m,
                CachedInputPricePerMillion = 0.08m
            }
        }
    };
    
    /// <summary>
    /// 智谱 AI (Zhipu)
    /// Base URL: https://open.bigmodel.cn/api/paas/v4/
    /// 价格参考: https://open.bigmodel.cn/pricing
    /// </summary>
    public static ModelProviderOptions Zhipu => new()
    {
        Name = "Zhipu",
        BaseUrl = "https://open.bigmodel.cn/api/paas/v4/",
        ApiKeyEnvironmentVariable = "ZHIPU_API_KEY",
        Models = new Dictionary<ModelCapability, string>
        {
            [ModelCapability.Large] = "glm-4-plus",
            [ModelCapability.Medium] = "glm-4-flash",
            [ModelCapability.Small] = "glm-4-flash",
            [ModelCapability.Reasoning] = "glm-4-plus",
            [ModelCapability.Coding] = "codegeex-4"
        },
        Pricing = new Dictionary<string, ModelPricing>
        {
            ["glm-4-plus"] = new()
            {
                ModelName = "glm-4-plus",
                InputPricePerMillion = 50m,
                OutputPricePerMillion = 50m,
                CachedInputPricePerMillion = null // 智谱暂不支持缓存计费
            },
            ["glm-4-flash"] = new()
            {
                ModelName = "glm-4-flash",
                InputPricePerMillion = 0.1m,
                OutputPricePerMillion = 0.1m,
                CachedInputPricePerMillion = null
            },
            ["codegeex-4"] = new()
            {
                ModelName = "codegeex-4",
                InputPricePerMillion = 0.1m,
                OutputPricePerMillion = 0.1m,
                CachedInputPricePerMillion = null
            }
        }
    };

    /// <summary>自定义 Provider</summary>
    public static ModelProviderOptions Custom(
        string name,
        string baseUrl,
        Dictionary<ModelCapability, string> models,
        Dictionary<string, ModelPricing>? pricing = null,
        string? apiKey = null,
        string? apiKeyEnvVar = null) => new()
    {
        Name = name,
        BaseUrl = baseUrl,
        ApiKey = apiKey,
        ApiKeyEnvironmentVariable = apiKeyEnvVar,
        Models = models,
        Pricing = pricing ?? new Dictionary<string, ModelPricing>()
    };
}
