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
            [ModelCapability.Large] = "qwen3.5-plus",
            [ModelCapability.Medium] = "qwen3.5-plus",
            [ModelCapability.Small] = "qwen-turbo",
            [ModelCapability.Reasoning] = "qwen3.5-plus",
            [ModelCapability.Coding] = "qwen3.5-plus"
        },
        Pricing = new Dictionary<string, ModelPricing>
        {
            ["qwen3.5-plus"] = new()
            {
                ModelName = "qwen3.5-plus",
                InputPricePerMillion = 0.8m,
                OutputPricePerMillion = 4.8m,
                CachedInputPricePerMillion = 0.08m
            },
            ["qwen-turbo"] = new()
            {
                ModelName = "qwen-turbo",
                InputPricePerMillion = 0.3m,
                OutputPricePerMillion = 0.6m,
                CachedInputPricePerMillion = 0.03m
            }
        }
    };
    
    /// <summary>
    /// 智谱 AI (Zhipu)
    /// Base URL: https://open.bigmodel.cn/api/paas/v4/
    /// 模型参考: https://docs.bigmodel.cn/cn/guide/start/model-overview
    /// 价格参考: https://open.bigmodel.cn/pricing
    /// </summary>
    public static ModelProviderOptions Zhipu => new()
    {
        Name = "Zhipu",
        BaseUrl = "https://open.bigmodel.cn/api/paas/v4/",
        ApiKeyEnvironmentVariable = "ZHIPU_API_KEY",
        Models = new Dictionary<ModelCapability, string>
        {
            [ModelCapability.Large] = "glm-4.6",           // 最新旗舰基座模型，通用对话、推理与智能体能力全面升级
            [ModelCapability.Medium] = "glm-4.6",      // 高性价比，推理、编码和智能体任务表现强劲
            [ModelCapability.Small] = "glm-4.7-flash",     // 免费模型，最新基座模型的普惠版本
            [ModelCapability.Reasoning] = "glm-4.7",       // 旗舰模型用于复杂推理
            [ModelCapability.Coding] = "codegeex-4"        // 代码模型，适用于代码自动补全任务
        },
        Pricing = new Dictionary<string, ModelPricing>
        {
            ["glm-4.7"] = new()
            {
                ModelName = "glm-4.7",
                InputPricePerMillion = 50m,
                OutputPricePerMillion = 50m,
                CachedInputPricePerMillion = null
            },
            ["glm-4.6"] = new()
            {
                ModelName = "glm-4.6",
                InputPricePerMillion = 50m,
                OutputPricePerMillion = 50m,
                CachedInputPricePerMillion = null
            },
            ["glm-4.5-air"] = new()
            {
                ModelName = "glm-4.5-air",
                InputPricePerMillion = 2m,
                OutputPricePerMillion = 2m,
                CachedInputPricePerMillion = null
            },
            ["glm-4.5-airx"] = new()
            {
                ModelName = "glm-4.5-airx",
                InputPricePerMillion = 5m,
                OutputPricePerMillion = 5m,
                CachedInputPricePerMillion = null
            },
            ["glm-4.7-flash"] = new()
            {
                ModelName = "glm-4.7-flash",
                InputPricePerMillion = 0m,  // 免费模型
                OutputPricePerMillion = 0m,
                CachedInputPricePerMillion = null
            },
            ["glm-4-long"] = new()
            {
                ModelName = "glm-4-long",
                InputPricePerMillion = 1m,
                OutputPricePerMillion = 1m,
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
