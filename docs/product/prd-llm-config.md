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
