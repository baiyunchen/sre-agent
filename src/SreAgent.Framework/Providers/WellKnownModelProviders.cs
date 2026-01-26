namespace SreAgent.Framework.Providers;

/// <summary>
/// 预定义的 Model Provider 配置
/// 每个 Provider 都预设了各能力级别对应的模型
/// </summary>
public static class WellKnownModelProviders
{
    /// <summary>
    /// 阿里云百炼 (DashScope)
    /// Base URL: https://dashscope.aliyuncs.com/compatible-mode/v1
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
        }
    };
    
    /// <summary>
    /// 智谱 AI (Zhipu)
    /// Base URL: https://open.bigmodel.cn/api/paas/v4/
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
        }
    };

    /// <summary>自定义 Provider</summary>
    public static ModelProviderOptions Custom(
        string name,
        string baseUrl,
        Dictionary<ModelCapability, string> models,
        string? apiKey = null,
        string? apiKeyEnvVar = null) => new()
    {
        Name = name,
        BaseUrl = baseUrl,
        ApiKey = apiKey,
        ApiKeyEnvironmentVariable = apiKeyEnvVar,
        Models = models
    };
}
