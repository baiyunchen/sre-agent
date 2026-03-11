namespace SreAgent.Framework.Providers;

/// <summary>
/// 模型能力/用途分类
/// Agent 在定义时只需指定需要的能力级别，由 Provider 决定具体使用哪个模型
/// </summary>
public enum ModelCapability
{
    /// <summary>
    /// 大模型 - 用于复杂推理、综合分析、协调决策
    /// 特点：能力最强，成本最高，速度较慢
    /// 适用场景：诊断协调、根因分析、复杂决策
    /// </summary>
    Large,
    
    /// <summary>
    /// 中等模型 - 用于一般任务、工具调用、常规分析
    /// 特点：能力适中，成本适中，速度适中
    /// 适用场景：日志分析、指标查询、一般问答
    /// </summary>
    Medium,
    
    /// <summary>
    /// 小模型 - 用于简单任务、快速响应、批量处理
    /// 特点：能力基础，成本最低，速度最快
    /// 适用场景：文本提取、格式转换、简单分类
    /// </summary>
    Small,
    
    /// <summary>
    /// 推理模型 - 专门用于复杂推理和规划
    /// 特点：推理能力强，适合多步骤问题
    /// 适用场景：根因分析、故障诊断、复杂规划
    /// </summary>
    Reasoning,
    
    /// <summary>
    /// 代码模型 - 专门用于代码生成和理解
    /// 特点：代码能力强
    /// 适用场景：脚本生成、代码分析、配置生成
    /// </summary>
    Coding
}
