using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Abstractions;

/// <summary>
/// 工具接口 - 基础抽象
/// </summary>
public interface ITool
{
    /// <summary>工具名称（用于 LLM 调用，建议 snake_case）</summary>
    string Name { get; }
    
    /// <summary>工具简短描述（用于工具列表展示，一句话说明用途）</summary>
    string Summary { get; }
    
    /// <summary>工具详细描述（包含使用说明、参数说明等，在 GetToolDetail 时返回）</summary>
    string Description { get; }
    
    /// <summary>工具分类标签（用于分组展示）</summary>
    string Category { get; }
    
    /// <summary>
    /// 获取工具的详细信息（包含参数 Schema）
    /// 这个方法会在 LLM 请求工具详情时调用
    /// </summary>
    ToolDetail GetDetail();
    
    /// <summary>执行工具</summary>
    Task<ToolResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 工具详细信息
/// </summary>
public record ToolDetail
{
    /// <summary>工具名称</summary>
    public required string Name { get; init; }
    
    /// <summary>详细描述</summary>
    public required string Description { get; init; }
    
    /// <summary>参数 Schema（JSON Schema 格式的字符串）</summary>
    public required string ParameterSchema { get; init; }
    
    /// <summary>返回值说明</summary>
    public string? ReturnDescription { get; init; }
    
    /// <summary>使用示例</summary>
    public IReadOnlyList<ToolExample>? Examples { get; init; }
}

/// <summary>
/// 工具使用示例
/// </summary>
public record ToolExample
{
    /// <summary>示例描述</summary>
    public required string Description { get; init; }
    
    /// <summary>输入参数（JSON 格式）</summary>
    public required string Input { get; init; }
    
    /// <summary>预期输出</summary>
    public string? ExpectedOutput { get; init; }
}
