using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SreAgent.Framework.Abstractions;

/// <summary>
/// 将 ITool 适配为 Microsoft.Extensions.AI 的 AIFunction
/// 这是连接我们自定义工具系统与 Microsoft.Extensions.AI 的桥梁
/// 
/// 设计说明：
/// - 此类主要用于向 LLM 传递正确的工具定义（名称、描述、参数 Schema）
/// - 工具的实际执行仍然由 ToolLoopAgent.ExecuteToolCallsAsync 处理
/// - 这样设计是为了保持执行逻辑的集中管理，便于日志、监控和错误处理
/// </summary>
public class ToolAIFunction : AIFunction
{
    private readonly ITool _tool;
    private readonly JsonElement _jsonSchema;
    
    /// <summary>
    /// 创建 ToolAIFunction
    /// </summary>
    /// <param name="tool">要适配的 ITool</param>
    public ToolAIFunction(ITool tool)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        
        // 解析工具的参数 Schema
        var detail = tool.GetDetail();
        _jsonSchema = JsonDocument.Parse(detail.ParameterSchema).RootElement.Clone();
    }
    
    /// <inheritdoc />
    public override string Name => _tool.Name;
    
    /// <inheritdoc />
    public override string Description => _tool.Description;
    
    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;
    
    /// <summary>
    /// 获取原始的 ITool
    /// </summary>
    public ITool UnderlyingTool => _tool;
    
    /// <inheritdoc />
    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        // 注意：此方法不应被直接调用
        // 工具执行由 ToolLoopAgent.ExecuteToolCallsAsync 统一管理
        // 这里只是为了满足 AIFunction 的抽象要求
        throw new NotSupportedException(
            $"工具 '{Name}' 不支持通过 AIFunction.InvokeAsync 直接调用。" +
            "请使用 ITool.ExecuteAsync 或通过 ToolLoopAgent 执行。");
    }
}

/// <summary>
/// ITool 扩展方法
/// </summary>
public static class ToolExtensions
{
    /// <summary>
    /// 将 ITool 转换为 AIFunction
    /// </summary>
    /// <param name="tool">要转换的工具</param>
    /// <returns>AIFunction 实例</returns>
    public static ToolAIFunction ToAIFunction(this ITool tool)
    {
        return new ToolAIFunction(tool);
    }
    
    /// <summary>
    /// 将 ITool 转换为 AITool（AIFunction 的基类）
    /// </summary>
    /// <param name="tool">要转换的工具</param>
    /// <returns>AITool 实例</returns>
    public static AITool ToAITool(this ITool tool)
    {
        return new ToolAIFunction(tool);
    }
}
