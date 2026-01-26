using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;

namespace SreAgent.Framework.Agents;

/// <summary>
/// Token 管理器 - 负责 Token 估算和剪枝触发
/// </summary>
public class TokenManager
{
    private readonly ITokenEstimator _tokenEstimator;
    private readonly ILogger _logger;

    public TokenManager(ITokenEstimator tokenEstimator, ILogger? logger = null)
    {
        _tokenEstimator = tokenEstimator;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// 估算工具定义的 Token 数
    /// </summary>
    public int EstimateToolDefinitionTokens(IReadOnlyList<ITool> tools)
    {
        var totalTokens = 0;
        foreach (var tool in tools)
        {
            var detail = tool.GetDetail();
            // 工具名称 + 描述 + 参数 schema 的估算
            totalTokens += _tokenEstimator.EstimateTokens(detail.Name);
            totalTokens += _tokenEstimator.EstimateTokens(detail.Description);
            totalTokens += _tokenEstimator.EstimateTokens(detail.ParameterSchema);
        }
        return totalTokens;
    }

    /// <summary>
    /// 检查是否需要剪枝
    /// </summary>
    /// <param name="currentTokens">当前 Token 数（包含工具定义）</param>
    /// <param name="effectiveLimit">有效 Token 限制</param>
    /// <returns>是否需要剪枝</returns>
    public bool NeedsTrimming(int currentTokens, int effectiveLimit)
    {
        return currentTokens > effectiveLimit;
    }

    /// <summary>
    /// 计算当前总 Token 数
    /// </summary>
    /// <param name="contextManager">上下文管理器</param>
    /// <param name="tools">工具列表</param>
    /// <returns>当前总 Token 数</returns>
    public int CalculateTotalTokens(IContextManager contextManager, IReadOnlyList<ITool> tools)
    {
        var toolDefinitionTokens = EstimateToolDefinitionTokens(tools);
        return contextManager.EstimatedTokenCount + toolDefinitionTokens;
    }

    /// <summary>
    /// 尝试进行剪枝
    /// </summary>
    /// <param name="contextManager">上下文管理器</param>
    /// <param name="trimmer">剪枝器</param>
    /// <param name="effectiveLimit">有效 Token 限制</param>
    /// <param name="trimTargetRatio">剪枝目标比例</param>
    /// <param name="tools">工具列表</param>
    /// <returns>剪枝结果</returns>
    public TrimResult TryTrim(
        IContextManager contextManager,
        IContextTrimmer trimmer,
        int effectiveLimit,
        double trimTargetRatio,
        IReadOnlyList<ITool> tools)
    {
        var toolDefinitionTokens = EstimateToolDefinitionTokens(tools);
        var targetTokens = (int)(effectiveLimit * trimTargetRatio) - toolDefinitionTokens;

        _logger.LogDebug(
            "开始剪枝，目标 Token: {TargetTokens}，有效限制: {EffectiveLimit}，工具定义 Token: {ToolTokens}",
            targetTokens, effectiveLimit, toolDefinitionTokens);

        var result = trimmer.Trim(contextManager, targetTokens);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "剪枝完成，Token: {Before} -> {After}，移除消息: {Removed}，策略: {Strategy}",
                result.TokensBefore, result.TokensAfter, result.MessagesRemoved, result.StrategyUsed);
        }
        else
        {
            _logger.LogWarning("剪枝失败: {Error}", result.ErrorMessage);
        }

        return result;
    }
}
