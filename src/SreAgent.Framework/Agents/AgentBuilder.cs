using Microsoft.Extensions.Logging;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Options;
using SreAgent.Framework.Providers;

namespace SreAgent.Framework.Agents;

/// <summary>
/// Agent 构建器 - 提供流畅的 API 来创建 Agent
/// 注：剪枝配置已移至 ContextManagerOptions
/// </summary>
public class AgentBuilder
{
    private readonly string _id;
    private string _name;
    private string _description = string.Empty;
    private string? _systemPrompt;
    private ModelCapability _modelCapability = ModelCapability.Medium;
    private int _maxIterations = 10;
    private double _temperature = 0.7;
    private int? _maxTokens;
    private readonly List<ITool> _tools = [];
    private ILogger<ToolLoopAgent>? _logger;

    private AgentBuilder(string id)
    {
        _id = id;
        _name = id;
    }

    /// <summary>创建 Agent 构建器</summary>
    public static AgentBuilder Create(string id) => new(id);

    /// <summary>设置 Agent 名称</summary>
    public AgentBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>设置 Agent 描述</summary>
    public AgentBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>设置 System Prompt</summary>
    public AgentBuilder WithSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    /// <summary>设置模型能力级别</summary>
    public AgentBuilder WithModelCapability(ModelCapability capability)
    {
        _modelCapability = capability;
        return this;
    }

    /// <summary>设置最大迭代次数</summary>
    public AgentBuilder WithMaxIterations(int maxIterations)
    {
        _maxIterations = maxIterations;
        return this;
    }

    /// <summary>设置温度参数</summary>
    public AgentBuilder WithTemperature(double temperature)
    {
        _temperature = temperature;
        return this;
    }

    /// <summary>设置最大输出 Token 数</summary>
    public AgentBuilder WithMaxTokens(int maxTokens)
    {
        _maxTokens = maxTokens;
        return this;
    }

    /// <summary>添加工具</summary>
    public AgentBuilder WithTool(ITool tool)
    {
        _tools.Add(tool);
        return this;
    }

    /// <summary>添加多个工具</summary>
    public AgentBuilder WithTools(params ITool[] tools)
    {
        _tools.AddRange(tools);
        return this;
    }

    /// <summary>添加多个工具</summary>
    public AgentBuilder WithTools(IEnumerable<ITool> tools)
    {
        _tools.AddRange(tools);
        return this;
    }

    /// <summary>添加子 Agent（自动包装为 Tool）</summary>
    public AgentBuilder WithSubAgent(IAgent subAgent)
    {
        _tools.Add(new SubAgentTool(subAgent));
        return this;
    }

    /// <summary>添加多个子 Agent</summary>
    public AgentBuilder WithSubAgents(params IAgent[] subAgents)
    {
        foreach (var agent in subAgents)
        {
            _tools.Add(new SubAgentTool(agent));
        }
        return this;
    }

    /// <summary>设置日志记录器</summary>
    public AgentBuilder WithLogger(ILogger<ToolLoopAgent>? logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>构建 Agent</summary>
    public ToolLoopAgent Build(ModelProvider modelProvider)
    {
        var options = new AgentOptions
        {
            SystemPrompt = _systemPrompt,
            ModelCapability = _modelCapability,
            MaxIterations = _maxIterations,
            Temperature = _temperature,
            MaxTokens = _maxTokens,
            Tools = _tools
        };

        return new ToolLoopAgent(
            _id,
            _name,
            _description,
            modelProvider,
            options,
            _logger);
    }
}
