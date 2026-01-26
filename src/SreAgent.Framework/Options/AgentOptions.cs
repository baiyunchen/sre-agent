using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Providers;

namespace SreAgent.Framework.Options;

/// <summary>
/// Agent 配置选项，用于自定义 Agent 行为
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// 模型能力级别
    /// Agent 只需指定需要的能力级别，具体模型由 Provider 决定
    /// </summary>
    public ModelCapability ModelCapability { get; set; } = ModelCapability.Medium;
    
    /// <summary>System Prompt</summary>
    public string? SystemPrompt { get; set; }
    
    /// <summary>最大迭代次数（Tool Loop 的最大轮数）</summary>
    public int MaxIterations { get; set; } = 10;
    
    /// <summary>温度参数</summary>
    public double Temperature { get; set; } = 0.7;
    
    /// <summary>单次请求最大 Token</summary>
    public int? MaxTokens { get; set; }
    
    /// <summary>执行超时时间</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>可用的工具列表</summary>
    public IReadOnlyList<ITool> Tools { get; set; } = [];
    
    /// <summary>
    /// 上下文剪枝器
    /// 如果不指定，使用 RemoveOldestContextTrimmer 作为默认实现
    /// </summary>
    public IContextTrimmer? ContextTrimmer { get; set; }
    
    /// <summary>剪枝后的目标 Token 比例（相对于模型限制）</summary>
    public double TrimTargetRatio { get; set; } = 0.8;
}
