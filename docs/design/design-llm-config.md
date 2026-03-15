# Design: LLM Configuration Settings

## 设计概述

重新设计 Settings > LLM Configuration 标签页，从纯静态 UI 升级为与后端集成的动态配置管理界面。核心改变：将前端"单模型选择"改为后端"能力级别 → 模型映射"展示。

## 关键界面

### LLM Configuration 主界面

**布局（单 Card）：**

1. **Provider 选择区**
   - Provider 下拉选择器：AliyunBailian / Zhipu
   - Provider 信息展示：Base URL（只读）
   
2. **API Key 配置区**
   - API Key 输入框（password 类型，带 show/hide 切换）
   - 当前 key 仅以 masked placeholder 展示（如 `Current: sk-***0243`）
   - 当用户切换到新 Provider 时，状态 Badge 显示 `Required for new provider`，并强制输入新 key 才能保存

3. **模型映射区（Capability → Model）**
   - 以分组下拉形式展示 5 种能力级别及其对应模型：
     - Large (复杂推理) → qwen3.5-plus
     - Medium (一般任务) → qwen3.5-plus
     - Small (快速响应) → qwen-turbo
     - Reasoning (推理规划) → qwen3.5-plus
     - Coding (代码生成) → qwen3.5-plus
   - 支持编辑：每个 capability 可从 provider 的 `availableModels` 里选择

4. **操作按钮**
   - LLM Card 内只有一个 Save Changes
   - 全局页面 Save 按钮在 LLM tab 隐藏，避免双保存入口冲突

## 交互状态

### 加载态
- Card 内部显示 Skeleton 占位

### 成功态
- 表单填充后端返回数据
- Save 后 toast 提示 "LLM configuration updated successfully"

### 错误态
- API 失败时在 Card 内展示错误信息 + 重试按钮
- 切换 provider 未输入 key 时，点击保存给出明确错误 toast，不触发成功提示

### 空态
- 不适用（总有默认配置）

## 复用组件

- `Card`, `CardHeader`, `CardTitle`, `CardDescription`, `CardContent` (shadcn)
- `Label`, `Input`, `Select`, `Button`, `Badge` (shadcn)
- `Skeleton` (shadcn, 加载态)
- `toast` (sonner)
- 沿用现有 Settings 页面整体布局和 Tabs 结构

## 与后端对齐

- 移除前端原有的 Temperature / Max Tokens 字段（后端不支持此粒度配置）
- 移除单一 Model Name 下拉（改为 Capability → Model 映射展示）
- Provider 选项对齐后端 `WellKnownModelProviders`
- API Key 仅在 PUT 时传入，GET 返回遮盖后的值

## 回归补充（US-L07：Tools 与 Approvals 入口收敛）

### Tools 页面

- Tools 列表由后端 `GET /api/tools` 驱动，不再使用前端静态 mock
- 每个工具卡片展示：`name`、`category`、`summary`、`invocations`、`successRate`、`avgDurationMs`
- 规则操作改为每个工具一处 `Auto Approve` 开关：
  - 开启：`auto-approve`（写入 `always-allow`）
  - 关闭：`require-approval`
- 若后端返回 `always-deny`，UI 显示 `Always Deny` 状态文案

### Approvals 页面

- 保留 `Pending` 和 `History` 两个 tab
- 移除 `Rules` tab，避免与 Tools 页形成双入口
