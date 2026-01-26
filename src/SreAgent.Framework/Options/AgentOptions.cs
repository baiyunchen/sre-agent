using SreAgent.Framework.Abstractions;

namespace SreAgent.Framework.Options;

/// <summary>
/// Agent 配置选项，用于自定义 Agent 行为
/// </summary>
public class AgentOptions
{
    /// <summary>使用的模型标识</summary>
    public string Model { get; set; } = "gpt-4";
    
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
}
