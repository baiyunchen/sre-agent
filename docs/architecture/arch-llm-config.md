# Architecture: LLM Configuration Settings

## 需求映射

| User Story | 技术方案 |
|-----------|---------|
| US-L01 查看 LLM 配置 | `GET /api/settings/llm` 返回当前 Provider 配置 |
| US-L02 切换 Provider | `PUT /api/settings/llm` 更新 Provider + 运行时热切换 |
| US-L03 配置 API Key | `PUT /api/settings/llm` 接受 apiKey 字段 |
| US-L04 查看可用 Providers | `GET /api/settings/llm/providers` 返回已知 Provider 列表 |
| US-L05 保存校验补强 | 切换 Provider 必须显式输入新 key；models 覆盖做 capability/model 合法性校验 |
| US-L06 配置持久化 | 新增 `llm_settings` 表 + 启动恢复流程，重启后仍生效 |
| US-L07 Tools 统一审批开关 | `GET /api/tools` 返回工具注册表；`PUT /api/tools/{toolName}/approval-mode` 切换 auto-approve |

## 模块拆分

### 后端

1. **IModelProviderAccessor** (Framework 层新增接口)
   - `ModelProvider Current { get; }` — 获取当前 Provider
   - `void Update(ModelProviderOptions options)` — 运行时切换
   - 替代当前 `ModelProvider` 直接注入

2. **ModelProviderAccessor** (Api 层实现)
   - 持有 `ModelProvider` 实例 + ReaderWriterLockSlim
   - 初始化时从默认值创建，启动阶段可由 `LlmSettingsService` 从数据库恢复覆盖
   - `Update()` 创建新的 `ModelProvider` 替换旧实例

3. **LlmSettingsRepository** (Repository 层新增)
   - 对应表：`llm_settings`
   - 负责单例配置（id=1）的读取与 upsert

4. **LlmSettingsService** (Application 层新增)
   - 聚合保存校验逻辑（provider 切换、apiKey 约束、models 覆盖合法性）
   - 写入数据库并同步更新 `IModelProviderAccessor`
   - 启动阶段执行持久化恢复

5. **SettingsController** (Api 层)
   - 仅负责 HTTP 入口、参数绑定、错误码映射
   - 核心业务逻辑下沉至 `ILlmSettingsService`

6. **ToolsController** (Api 层新增)
   - 对外提供工具注册表查询与 auto-approve 切换接口
   - 聚合 `IAgent.Options.Tools`（工具清单）+ `IToolInvocationRepository`（统计）+ `IApprovalService`（规则）

### 前端

1. **Types** — `LlmConfigResponse`, `LlmConfigUpdateRequest`, `LlmProviderInfo`
2. **API Functions** — `fetchLlmConfig()`, `updateLlmConfig()`, `fetchLlmProviders()`
3. **Hook** — `useLlmConfig()` (React Query)
4. **SettingsPage LLM Tab** — 对接真实数据
5. **ToolsPage** — 对接工具注册表并支持 per-tool Auto Approve 开关
6. **ApprovalsPage** — 移除 Rules tab，仅保留 Pending/History

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
校验规则：
- 切换 provider 时必须提供 `apiKey`
- `models` 覆盖中的 capability 必须是已知枚举，model 必须属于 provider 的 `availableModels`

### GET /api/settings/llm/providers

```json
{
  "providers": [
    {
      "name": "AliyunBailian",
      "displayName": "Aliyun Bailian (通义千问)",
      "baseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
      "models": { "Large": "qwen3.5-plus", ... },
      "availableModels": ["qwen3.5-plus", "qwen-turbo"]
    },
    {
      "name": "Zhipu",
      "displayName": "Zhipu AI (智谱清言)",
      "baseUrl": "https://open.bigmodel.cn/api/paas/v4/",
      "models": { "Large": "glm-4.6", ... },
      "availableModels": ["glm-4.6", "glm-4.7", "codegeex-4", "..."]
    }
  ]
}
```

### GET /api/tools

```json
{
  "items": [
    {
      "name": "cloudwatch_simple_query",
      "summary": "Search CloudWatch logs by time range and keyword",
      "category": "AWS Monitoring",
      "approvalMode": "auto-approve",
      "autoApprove": true,
      "invocations": 61,
      "successRate": 80.3,
      "avgDurationMs": 1135
    }
  ],
  "total": 8
}
```

### PUT /api/tools/{toolName}/approval-mode

```json
{
  "autoApprove": false,
  "updatedBy": "oncall-user"
}
```

响应：`ToolRegistryItem`  
规则映射：
- `autoApprove=true` -> `always-allow`
- `autoApprove=false` -> `require-approval`

## 关键数据流

1. 前端 → `GET /api/settings/llm` → 展示当前配置
2. 前端 → `PUT /api/settings/llm` → SettingsController → LlmSettingsService → LlmSettingsRepository.Upsert + ModelProviderAccessor.Update()
3. 服务启动 → `LlmSettingsService.InitializeFromPersistenceAsync()` → 加载 `llm_settings` 覆盖默认配置
4. Agent → `IModelProviderAccessor.Current` → 获取最新 Provider
5. 前端 Tools 页面 → `GET /api/tools` → 聚合工具元数据 + 调用统计 + 审批模式
6. 前端切换 Auto Approve → `PUT /api/tools/{toolName}/approval-mode` → `ApprovalService.UpsertRuleAsync` → `approval_rules`

## 设计决策

- **引入 DB 持久化**：新增 `llm_settings` 单例表，保证重启后配置可恢复。
- **分层治理**：Controller 保持薄层，规则与状态变更集中在 Application Service。
- **IModelProviderAccessor**：仍作为运行时热切换入口，Service 负责与持久化状态一致性。
- **API Key 安全**：GET 不返回完整 Key，仅返回遮盖提示；PUT 接受新 Key。
- **审批入口收敛**：将规则管理入口从 Approvals 迁移至 Tools，降低操作分散度。
