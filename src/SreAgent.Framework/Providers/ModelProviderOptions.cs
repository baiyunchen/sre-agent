namespace SreAgent.Framework.Providers;

/// <summary>
/// Model Provider 配置选项
/// 包含 API 连接信息、各能力级别对应的模型映射和定价信息
/// </summary>
public class ModelProviderOptions
{
    /// <summary>Provider 名称（用于标识）</summary>
    public required string Name { get; init; }
    
    /// <summary>API Base URL</summary>
    public required string BaseUrl { get; init; }
    
    /// <summary>API Key（直接配置）</summary>
    public string? ApiKey { get; init; }
    
    /// <summary>API Key 对应的环境变量名</summary>
    public string? ApiKeyEnvironmentVariable { get; init; }
    
    /// <summary>
    /// 各能力级别对应的模型映射
    /// Key: ModelCapability, Value: 模型名称
    /// </summary>
    public required Dictionary<ModelCapability, string> Models { get; init; }
    
    /// <summary>
    /// 各模型的定价信息
    /// Key: 模型名称, Value: 定价信息
    /// </summary>
    public Dictionary<string, ModelPricing> Pricing { get; init; } = new();
    
    /// <summary>获取指定能力级别对应的模型</summary>
    public string GetModel(ModelCapability capability)
    {
        if (Models.TryGetValue(capability, out var model))
        {
            return model;
        }
        
        // 降级策略：如果没有指定能力的模型，尝试使用 Medium，再尝试 Large
        if (capability != ModelCapability.Medium && Models.TryGetValue(ModelCapability.Medium, out model))
        {
            return model;
        }
        
        if (capability != ModelCapability.Large && Models.TryGetValue(ModelCapability.Large, out model))
        {
            return model;
        }
        
        throw new InvalidOperationException(
            $"No model configured for capability '{capability}' in provider '{Name}'.");
    }
    
    /// <summary>获取指定模型的定价信息</summary>
    public ModelPricing? GetPricing(string modelName)
    {
        return Pricing.TryGetValue(modelName, out var pricing) ? pricing : null;
    }
    
    /// <summary>获取指定能力级别对应模型的定价信息</summary>
    public ModelPricing? GetPricing(ModelCapability capability)
    {
        var modelName = GetModel(capability);
        return GetPricing(modelName);
    }
    
    /// <summary>计算成本</summary>
    public CostSummary? CalculateCost(string modelName, TokenUsageDetail usage)
    {
        var pricing = GetPricing(modelName);
        if (pricing == null) return null;
        
        var inputCost = usage.NonCachedInputTokens * pricing.InputPricePerMillion / 1_000_000m;
        var outputCost = usage.OutputTokens * pricing.OutputPricePerMillion / 1_000_000m;
        
        var cachedCost = pricing.CachedInputPricePerMillion.HasValue
            ? usage.CachedInputTokens * pricing.CachedInputPricePerMillion.Value / 1_000_000m
            : 0m;
        
        // 计算缓存节省的成本
        var cacheSavings = pricing.CachedInputPricePerMillion.HasValue
            ? usage.CachedInputTokens * (pricing.InputPricePerMillion - pricing.CachedInputPricePerMillion.Value) / 1_000_000m
            : 0m;
        
        return new CostSummary
        {
            ModelName = modelName,
            Usage = usage,
            InputCost = inputCost + cachedCost,
            OutputCost = outputCost,
            CacheSavings = cacheSavings
        };
    }
    
    /// <summary>获取有效的 API Key</summary>
    public string GetApiKey()
    {
        if (!string.IsNullOrEmpty(ApiKey))
        {
            return ApiKey;
        }
        
        if (!string.IsNullOrEmpty(ApiKeyEnvironmentVariable))
        {
            var envValue = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
            if (!string.IsNullOrEmpty(envValue))
            {
                return envValue;
            }
        }
        
        throw new InvalidOperationException(
            $"API Key not configured for provider '{Name}'. " +
            $"Please set ApiKey directly or configure environment variable '{ApiKeyEnvironmentVariable}'.");
    }
}
