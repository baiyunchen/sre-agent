# Architecture: LLM Configuration Settings

## 需求映射

| User Story | 技术方案 |
|-----------|---------|
| US-L01 查看 LLM 配置 | `GET /api/settings/llm` 返回当前 Provider 配置 |
| US-L02 切换 Provider | `PUT /api/settings/llm` 更新 Provider + 运行时热切换 |
| US-L03 配置 API Key | `PUT /api/settings/llm` 接受 apiKey 字段 |
| US-L04 查看可用 Providers | `GET /api/settings/llm/providers` 返回已知 Provider 列表 |

## 模块拆分

### 后端

1. **IModelProviderAccessor** (Framework 层新增接口)
   - `ModelProvider Current { get; }` — 获取当前 Provider
   - `void Update(ModelProviderOptions options)` — 运行时切换
   - 替代当前 `ModelProvider` 直接注入

2. **ModelProviderAccessor** (Api 层实现)
   - 持有 `ModelProvider` 实例 + ReaderWriterLockSlim
   - 初始化时从内存默认值创建（不引入 DB 持久化，保持简单）
   - `Update()` 创建新的 `ModelProvider` 替换旧实例

3. **SettingsController** (Api 层新增)
   - `GET /api/settings/llm` — 读取当前配置
   - `PUT /api/settings/llm` — 更新配置（切换 Provider / 设置 API Key）
   - `GET /api/settings/llm/providers` — 返回可用 Provider 列表

### 前端

1. **Types** — `LlmConfigResponse`, `LlmConfigUpdateRequest`, `LlmProviderInfo`
2. **API Functions** — `fetchLlmConfig()`, `updateLlmConfig()`, `fetchLlmProviders()`
3. **Hook** — `useLlmConfig()` (React Query)
4. **SettingsPage LLM Tab** — 对接真实数据

## API 设计

### GET /api/settings/llm

```json
{
  "provider": "AliyunBailian",
  "baseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
  "apiKeyConfigured": true,
  "apiKeyHint": "***SCOPE_API_KEY",
  "models": {
    "Large": "qwen3.5-plus",
    "Medium": "qwen3.5-plus",
    "Small": "qwen-turbo",
    "Reasoning": "qwen3.5-plus",
    "Coding": "qwen3.5-plus"
  }
}
```

### PUT /api/settings/llm

```json
{
  "provider": "Zhipu",
  "apiKey": "sk-xxx"
}
```

响应：同 GET 结构

### GET /api/settings/llm/providers

```json
{
  "providers": [
    {
      "name": "AliyunBailian",
      "displayName": "Aliyun Bailian (通义千问)",
      "baseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
      "models": { "Large": "qwen3.5-plus", ... }
    },
    {
      "name": "Zhipu",
      "displayName": "Zhipu AI (智谱清言)",
      "baseUrl": "https://open.bigmodel.cn/api/paas/v4/",
      "models": { "Large": "glm-4.6", ... }
    }
  ]
}
```

## 关键数据流

1. 前端 → `GET /api/settings/llm` → 展示当前配置
2. 前端 → `PUT /api/settings/llm` → SettingsController → ModelProviderAccessor.Update() → 后续 Agent 使用新 Provider
3. Agent → IModelProviderAccessor.Current → 获取最新 Provider

## 设计决策

- **不引入 DB 持久化**：LLM 配置重启后回到默认值（AliyunBailian），与当前行为一致。持久化在后续迭代加入。
- **IModelProviderAccessor 替代直接注入 ModelProvider**：允许运行时切换，同时保持向下兼容。
- **API Key 安全**：GET 不返回完整 Key，仅返回遮盖提示；PUT 接受新 Key。
