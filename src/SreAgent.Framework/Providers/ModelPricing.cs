namespace SreAgent.Framework.Providers;

/// <summary>
/// 模型定价信息
/// 价格单位：每百万 Token 的费用（人民币元）
/// </summary>
public record ModelPricing
{
    /// <summary>模型名称</summary>
    public required string ModelName { get; init; }
    
    /// <summary>输入 Token 价格（元/百万 Token）</summary>
    public required decimal InputPricePerMillion { get; init; }
    
    /// <summary>输出 Token 价格（元/百万 Token）</summary>
    public required decimal OutputPricePerMillion { get; init; }
    
    /// <summary>缓存命中的输入 Token 价格（元/百万 Token），null 表示不支持缓存</summary>
    public decimal? CachedInputPricePerMillion { get; init; }
    
    /// <summary>计算成本</summary>
    /// <param name="inputTokens">输入 Token 数</param>
    /// <param name="outputTokens">输出 Token 数</param>
    /// <param name="cachedInputTokens">缓存命中的输入 Token 数</param>
    /// <returns>总成本（元）</returns>
    public decimal CalculateCost(long inputTokens, long outputTokens, long cachedInputTokens = 0)
    {
        var nonCachedInputTokens = inputTokens - cachedInputTokens;
        
        var inputCost = nonCachedInputTokens * InputPricePerMillion / 1_000_000m;
        var outputCost = outputTokens * OutputPricePerMillion / 1_000_000m;
        var cachedCost = CachedInputPricePerMillion.HasValue 
            ? cachedInputTokens * CachedInputPricePerMillion.Value / 1_000_000m 
            : 0m;
        
        return inputCost + outputCost + cachedCost;
    }
}

/// <summary>
/// Token 使用量详情（支持缓存统计）
/// </summary>
public record TokenUsageDetail
{
    /// <summary>输入 Token 数</summary>
    public long InputTokens { get; init; }
    
    /// <summary>输出 Token 数</summary>
    public long OutputTokens { get; init; }
    
    /// <summary>缓存命中的输入 Token 数</summary>
    public long CachedInputTokens { get; init; }
    
    /// <summary>总 Token 数</summary>
    public long TotalTokens => InputTokens + OutputTokens;
    
    /// <summary>非缓存的输入 Token 数</summary>
    public long NonCachedInputTokens => InputTokens - CachedInputTokens;
    
    /// <summary>缓存命中率</summary>
    public double CacheHitRate => InputTokens > 0 ? (double)CachedInputTokens / InputTokens : 0;
    
    public static TokenUsageDetail operator +(TokenUsageDetail a, TokenUsageDetail b) => new()
    {
        InputTokens = a.InputTokens + b.InputTokens,
        OutputTokens = a.OutputTokens + b.OutputTokens,
        CachedInputTokens = a.CachedInputTokens + b.CachedInputTokens
    };
    
    public static TokenUsageDetail Zero => new() { InputTokens = 0, OutputTokens = 0, CachedInputTokens = 0 };
}

/// <summary>
/// 成本统计结果
/// </summary>
public record CostSummary
{
    /// <summary>模型名称</summary>
    public required string ModelName { get; init; }
    
    /// <summary>Token 使用详情</summary>
    public required TokenUsageDetail Usage { get; init; }
    
    /// <summary>输入成本（元）</summary>
    public decimal InputCost { get; init; }
    
    /// <summary>输出成本（元）</summary>
    public decimal OutputCost { get; init; }
    
    /// <summary>缓存节省的成本（元）</summary>
    public decimal CacheSavings { get; init; }
    
    /// <summary>总成本（元）</summary>
    public decimal TotalCost => InputCost + OutputCost;
    
    /// <summary>如果没有缓存的总成本（用于计算节省比例）</summary>
    public decimal CostWithoutCache => TotalCost + CacheSavings;
    
    /// <summary>缓存节省比例</summary>
    public double CacheSavingsRate => CostWithoutCache > 0 ? (double)(CacheSavings / CostWithoutCache) : 0;
}
