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
   - Key 来源提示：显示 "From environment variable: DASHSCOPE_API_KEY" 或 "Configured via UI"
   - 配置状态 Badge：`Configured` (绿) / `Not Configured` (红)

3. **模型映射区（Capability → Model）**
   - 以表格或分组展示 5 种能力级别及其对应模型：
     - Large (复杂推理) → qwen3.5-plus
     - Medium (一般任务) → qwen3.5-plus
     - Small (快速响应) → qwen-turbo
     - Reasoning (推理规划) → qwen3.5-plus
     - Coding (代码生成) → qwen3.5-plus
   - 只读展示（由 Provider 决定），后续迭代可支持自定义

4. **操作按钮**
   - Save Changes：提交配置更新
   - Reset to Defaults：重置为默认配置

## 交互状态

### 加载态
- Card 内部显示 Skeleton 占位

### 成功态
- 表单填充后端返回数据
- Save 后 toast 提示 "LLM configuration updated successfully"

### 错误态
- API 失败时在 Card 内展示错误信息 + 重试按钮

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
