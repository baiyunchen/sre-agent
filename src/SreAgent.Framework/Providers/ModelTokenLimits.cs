namespace SreAgent.Framework.Providers;

/// <summary>
/// 模型 Token 限制配置
/// </summary>
public class ModelTokenLimits
{
    /// <summary>模型最大上下文窗口（输入 + 输出）</summary>
    public int MaxContextTokens { get; init; } = 128000;
    
    /// <summary>模型最大输出 Token 数</summary>
    public int MaxOutputTokens { get; init; } = 8192;
    
    /// <summary>
    /// 预留 Buffer 比例（0-1）
    /// 用于预留一定空间给输出和估算误差
    /// </summary>
    public double ReservedBufferRatio { get; init; } = 0.1;
    
    /// <summary>
    /// 计算可用于输入的最大 Token 数
    /// = MaxContextTokens - MaxOutputTokens - Buffer
    /// </summary>
    public int EffectiveInputTokens
    {
        get
        {
            var buffer = (int)(MaxContextTokens * ReservedBufferRatio);
            return MaxContextTokens - MaxOutputTokens - buffer;
        }
    }
    
    /// <summary>默认配置（适用于大多数现代模型）</summary>
    public static ModelTokenLimits Default => new()
    {
        MaxContextTokens = 128000,
        MaxOutputTokens = 8192,
        ReservedBufferRatio = 0.1
    };
    
    /// <summary>Claude 3.5 Sonnet 配置</summary>
    public static ModelTokenLimits Claude35Sonnet => new()
    {
        MaxContextTokens = 200000,
        MaxOutputTokens = 8192,
        ReservedBufferRatio = 0.1
    };
    
    /// <summary>GPT-4o 配置</summary>
    public static ModelTokenLimits Gpt4o => new()
    {
        MaxContextTokens = 128000,
        MaxOutputTokens = 16384,
        ReservedBufferRatio = 0.1
    };
}
