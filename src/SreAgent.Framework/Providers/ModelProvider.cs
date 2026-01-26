using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace SreAgent.Framework.Providers;

/// <summary>
/// 全局 Model Provider 管理器
/// 负责管理当前使用的 Provider 配置，并根据能力级别创建 ChatClient
/// </summary>
public class ModelProvider
{
    private readonly ModelProviderOptions _options;
    private readonly Dictionary<ModelCapability, IChatClient> _clientCache = new();
    private readonly object _lock = new();
    
    /// <summary>当前 Provider 名称</summary>
    public string Name => _options.Name;
    
    /// <summary>Provider 配置</summary>
    public ModelProviderOptions Options => _options;
    
    /// <summary>
    /// 创建 ModelProvider 实例
    /// </summary>
    /// <param name="options">Provider 配置</param>
    public ModelProvider(ModelProviderOptions options)
    {
        _options = options;
    }
    
    /// <summary>
    /// 获取指定能力级别的 ChatClient
    /// </summary>
    /// <param name="capability">模型能力级别</param>
    /// <returns>IChatClient 实例（会缓存复用）</returns>
    public IChatClient GetChatClient(ModelCapability capability)
    {
        lock (_lock)
        {
            if (_clientCache.TryGetValue(capability, out var cachedClient))
            {
                return cachedClient;
            }
            
            var client = CreateChatClient(capability);
            _clientCache[capability] = client;
            return client;
        }
    }
    
    /// <summary>
    /// 获取指定能力级别对应的模型名称
    /// </summary>
    public string GetModelName(ModelCapability capability) => _options.GetModel(capability);
    
    /// <summary>
    /// 获取指定能力级别对应模型的定价信息
    /// </summary>
    public ModelPricing? GetPricing(ModelCapability capability) => _options.GetPricing(capability);
    
    /// <summary>
    /// 计算成本
    /// </summary>
    /// <param name="capability">模型能力级别</param>
    /// <param name="usage">Token 使用详情</param>
    /// <returns>成本统计结果</returns>
    public CostSummary? CalculateCost(ModelCapability capability, TokenUsageDetail usage)
    {
        var modelName = GetModelName(capability);
        return _options.CalculateCost(modelName, usage);
    }
    
    /// <summary>
    /// 计算成本
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <param name="usage">Token 使用详情</param>
    /// <returns>成本统计结果</returns>
    public CostSummary? CalculateCost(string modelName, TokenUsageDetail usage)
    {
        return _options.CalculateCost(modelName, usage);
    }
    
    private IChatClient CreateChatClient(ModelCapability capability)
    {
        var apiKey = _options.GetApiKey();
        var modelId = _options.GetModel(capability);
        
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(_options.BaseUrl)
        };
        
        var credential = new ApiKeyCredential(apiKey);
        var openAiClient = new OpenAIClient(credential, options);
        var chatClient = openAiClient.GetChatClient(modelId);
        
        return chatClient.AsIChatClient();
    }
    
    #region 静态工厂方法
    
    /// <summary>使用阿里云百炼创建 Provider</summary>
    public static ModelProvider AliyunBailian() => new(WellKnownModelProviders.AliyunBailian);
    
    /// <summary>使用智谱 AI 创建 Provider</summary>
    public static ModelProvider Zhipu() => new(WellKnownModelProviders.Zhipu);
    
    /// <summary>使用自定义配置创建 Provider</summary>
    public static ModelProvider Custom(ModelProviderOptions options) => new(options);
    
    #endregion
}
