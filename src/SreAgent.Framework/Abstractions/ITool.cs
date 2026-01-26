using System.Text.Json;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Abstractions;

/// <summary>
/// 工具接口
/// </summary>
public interface ITool
{
    /// <summary>工具名称（用于 LLM 调用，建议 snake_case）</summary>
    string Name { get; }
    
    /// <summary>工具描述（帮助 LLM 理解何时使用）</summary>
    string Description { get; }
    
    /// <summary>参数 JSON Schema</summary>
    JsonElement ParameterSchema { get; }
    
    /// <summary>执行工具</summary>
    Task<ToolResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
