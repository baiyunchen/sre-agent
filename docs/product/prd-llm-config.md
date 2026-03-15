# PRD: LLM Configuration Settings

## 背景

SRE Agent 系统使用 LLM 进行告警分析与诊断。当前 LLM Provider 在代码中硬编码为阿里云百炼，管理员无法通过 UI 查看或修改 LLM 配置。Settings 页面存在 LLM Configuration 标签但仅为静态展示，与后端无任何集成。

## 用户故事

### US-L01: 查看当前 LLM 配置

作为系统管理员，我希望在 Settings 页面查看当前 LLM Provider 配置（提供商、各能力对应模型、API Key 配置状态），以便了解系统当前使用的 AI 模型配置。

**验收标准：**
- Given 系统已启动 When 管理员打开 Settings > LLM Configuration Then 显示当前 Provider 名称、Base URL、API Key 状态（已配置/未配置，不显示明文）、各能力级别对应的模型名称
- Given 后端 LLM 配置不可用 When 加载 LLM Settings Then 显示错误状态而非白屏

### US-L02: 切换 LLM Provider

作为系统管理员，我希望能切换 LLM Provider（如从阿里云百炼切换到智谱 AI），以便根据成本与效果选择最优模型。

**验收标准：**
- Given 管理员在 LLM Configuration 页面 When 选择不同 Provider When 点击 Save Then 后端切换到新 Provider，后续 Agent 使用新配置
- Given Provider 切换成功 When 查看配置 Then 各能力级别模型映射更新为新 Provider 默认值
- Given API Key 未配置 When 切换 Provider 并保存 Then 提示需要配置 API Key

### US-L03: 配置 API Key

作为系统管理员，我希望通过 UI 配置 LLM Provider 的 API Key，以便无需修改环境变量即可完成初始配置或密钥轮转。

**验收标准：**
- Given 管理员输入新的 API Key When 点击 Save Then API Key 被安全存储，后端使用新 Key
- Given API Key 已配置 When 查看 LLM Settings Then API Key 显示为遮盖状态（如 `sk-***xyz`），不暴露完整密钥

### US-L04: 查看可用 Provider 列表

作为系统管理员，我希望查看系统支持的 LLM Provider 列表及其默认模型配置，以便在切换前了解各 Provider 的能力。

**验收标准：**
- Given 管理员打开 Provider 选择器 When 下拉列表展开 Then 显示所有已知 Provider 及简要说明

## 范围边界

### 在范围内
- 查看当前 LLM 配置（只读）
- 切换 Provider（AliyunBailian / Zhipu）
- 配置 API Key
- 运行时生效（无需重启）
- 各能力级别模型映射展示

### 不在范围内
- 自定义 Provider（自定义 Base URL + 模型映射）— 后续迭代
- 模型定价管理 — 保持后端默认
- Token 限制调整 — 保持后端默认
- Temperature / Max Tokens 前端配置 — 后端不支持此粒度，移除
- 多 Provider 并行（同时用两个 Provider）— 后续迭代

## 回归补充（US-L05 / US-L06）

### US-L05: 保存校验与用户预期对齐

作为系统管理员，我希望在切换 Provider 时必须显式输入新 API Key，以便避免“保存成功但实际不可用”的误导。

**验收标准：**
- Given 当前 Provider 为 Aliyun When 切换到 Zhipu 且未输入 `apiKey` Then 保存失败并提示必须输入新 key
- Given 当前 Provider 未变化 When 仅更新 models 且不输入 `apiKey` Then 保留当前 key，不得被清空
- Given `models` 覆盖包含未知 capability 或非法模型名 When 保存 Then 返回 400 并提示字段错误

### US-L06: 配置持久化与分层治理

作为系统管理员，我希望 LLM 配置能持久化到数据库并在服务重启后自动恢复，以便系统配置稳定可追踪。

**验收标准：**
- Given 管理员已保存 LLM 配置 When 服务重启 Then 读取到重启前配置
- Given 代码评审 When 检查 Settings 模块 Then 核心业务逻辑位于 Service/Repository，不堆积在 Controller
- Given 数据库迁移执行完成 When 检查 schema Then 存在 `llm_settings` 表

### US-L07: Tools 页面统一管理 Auto Approve

作为系统管理员，我希望在 Tools 页面直接管理每个工具是否自动审批，以便将审批规则入口从 Approvals 收敛到 Tools，减少配置路径分散。

**验收标准：**
- Given 管理员进入 `/tools` When 页面加载完成 Then 工具列表来自后端真实数据（名称、分类、调用统计、审批模式）
- Given 某工具当前为 require-approval When 管理员开启 Auto Approve Then 后端规则更新为 always-allow 且页面即时反映
- Given 某工具当前为 auto-approve When 管理员关闭 Auto Approve Then 后端规则更新为 require-approval 且页面即时反映
- Given 管理员进入 `/approvals` When 查看标签页 Then 仅展示 Pending 与 History，不再展示 Rules
