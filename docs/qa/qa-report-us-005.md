# US-005 E2E QA Report: POST /api/sessions/{sessionId}/messages 消息续聊

**Story**: 作为 SRE 工程师，我希望在 Session 详情页向运行中的 Agent 发消息，以便继续对话补充上下文

**验收**: 消息续聊链路验证，不要求触发 CloudWatch 告警：
1. 创建可继续会话（`POST /api/sre/chat`），记录 sessionId
2. 调用 `POST /api/sessions/{sessionId}/messages`，校验 200 且包含 `sessionId`、`isSuccess`、`tokenUsage`
3. 校验错误分支：message 为空返回 400，session 不存在返回 404

**执行时间**: 2026-03-12

---

## 执行摘要

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass**（4/4 用例通过） |
| **创建会话** | ✅ Pass（`POST /api/sre/chat` 返回 sessionId） |
| **POST /api/sessions/{id}/messages** | ✅ Pass（200，含 sessionId/isSuccess/tokenUsage） |
| **错误分支：空消息** | ✅ Pass（400，`message cannot be empty`） |
| **错误分支：session 不存在** | ✅ Pass（404） |

---

## 1. Pre-flight 检查

| 检查项 | 结果 | 证据 |
|--------|------|------|
| SRE Agent 健康 | ✅ | `curl http://localhost:5099/health` → `{"status":"Healthy"}` |

---

## 2. 执行步骤与结果

### 2.1 步骤汇总

| 步骤 | 命令/动作 | 结果 |
|------|-----------|------|
| 1. 创建会话 | `POST /api/sre/chat` 携带 `{"message":"简要介绍一下你自己"}` | ✅ 200，sessionId: `78b1a54e-4855-4445-9c74-cf39dc130055` |
| 2. 发送续聊消息 | `POST /api/sessions/{sessionId}/messages` 携带 `{"message":"你刚才说的 Playbook 知识库具体指什么？"}` | ✅ 200，含 sessionId/isSuccess/tokenUsage |
| 3. 空消息校验 | `POST /api/sessions/{sessionId}/messages` 携带 `{"message":""}` | ✅ 400，`{"error":"message cannot be empty"}` |
| 4. 不存在的 session | `POST /api/sessions/00000000-0000-0000-0000-000000000000/messages` | ✅ 404 |

### 2.2 关键命令

```bash
# 1. 创建可继续会话
curl -s -X POST http://localhost:5099/api/sre/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "简要介绍一下你自己，用一句话即可。"}'

# 2. 发送续聊消息（使用上一步返回的 sessionId）
SESSION_ID="78b1a54e-4855-4445-9c74-cf39dc130055"
curl -s -X POST "http://localhost:5099/api/sessions/${SESSION_ID}/messages" \
  -H "Content-Type: application/json" \
  -d '{"message": "你刚才说的 Playbook 知识库具体指什么？"}'

# 3. 错误分支：空消息
curl -s -X POST "http://localhost:5099/api/sessions/${SESSION_ID}/messages" \
  -H "Content-Type: application/json" \
  -d '{"message": ""}'

# 4. 错误分支：session 不存在
curl -s -X POST "http://localhost:5099/api/sessions/00000000-0000-0000-0000-000000000000/messages" \
  -H "Content-Type: application/json" \
  -d '{"message": "test message"}'
```

---

## 3. 校验结果

### 3.1 校验项汇总

| 用例 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| **创建会话** | 200，返回 sessionId | 200，sessionId: `78b1a54e-4855-4445-9c74-cf39dc130055` | ✅ Pass |
| **续聊消息** | 200，含 sessionId/isSuccess/tokenUsage | 200，含全部字段 | ✅ Pass |
| **空消息** | 400，错误信息 | 400，`{"error":"message cannot be empty"}` | ✅ Pass |
| **session 不存在** | 404 | 404 | ✅ Pass |

### 3.2 POST /api/sre/chat 响应（创建会话）

```json
{
  "sessionId": "78b1a54e-4855-4445-9c74-cf39dc130055",
  "output": "我是一个专业的 SRE 故障分析协调器...",
  "isSuccess": true,
  "error": null,
  "tokenUsage": {
    "promptTokens": 5936,
    "completionTokens": 144,
    "totalTokens": 6080
  }
}
```

### 3.3 POST /api/sessions/{sessionId}/messages 响应（200）

```json
{
  "sessionId": "78b1a54e-4855-4445-9c74-cf39dc130055",
  "output": "Playbook 知识库是一个 SRE 故障排除指南库...",
  "isSuccess": true,
  "error": null,
  "tokenUsage": {
    "promptTokens": 5135,
    "completionTokens": 171,
    "totalTokens": 5306
  }
}
```

**字段校验**：
- `sessionId` ✅
- `isSuccess` ✅
- `tokenUsage`（含 promptTokens/completionTokens/totalTokens）✅

### 3.4 错误分支：空消息（400）

```json
{"error":"message cannot be empty"}
```

空白字符消息同样返回 400。

### 3.5 错误分支：session 不存在（404）

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "traceId": "..."
}
```

---

## 4. 结论

- **US-005 POST /api/sessions/{sessionId}/messages 消息续聊**：**Pass**
- **链路**：`POST /api/sre/chat` 创建会话 → `POST /api/sessions/{sessionId}/messages` 续聊 → 200 且含 sessionId/isSuccess/tokenUsage
- **错误分支**：空消息 400、session 不存在 404 均符合预期

---

## 5. 前端最小回归（2026-03-12）

### 5.1 验证目标

验证 Session Detail 页面发送消息能力。

### 5.2 验证步骤

1) 打开 /sessions  
2) 点击第一条进入 /sessions/:id  
3) 在 Send Message 文本框输入一条消息并点击「发送消息」  
4) 校验页面出现 Agent 响应或错误反馈文本（至少一种可见），且页面无白屏  

### 5.3 验证结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开 /sessions | 页面可加载 | 页面加载成功，列表可见 | Pass |
| 2. 点击第一条进入 /sessions/:id | 详情页可加载 | 跳转至 `/sessions/b74478cf-1b23-45eb-a60f-26c7ac32d810` | Pass |
| 3. 输入消息并点击发送 | 文本框可输入，按钮可点击 | 输入「请简要总结当前会话的分析结论」，点击「发送消息」，按钮显示「发送中...」 | Pass |
| 4. Agent 响应或错误反馈 | 至少一种可见 | 出现错误反馈：「session status 'Failed' does not accept new messages」 | Pass |
| 5. 页面无白屏 | 无崩溃 | 页面正常渲染，无白屏 | Pass |

### 5.4 关键观察

- **sessionId 使用**：`b74478cf-1b23-45eb-a60f-26c7ac32d810`
- **发送流程**：Send Message 区块可见，textarea 可输入，发送按钮可点击，点击后显示「发送中...」
- **反馈**：该 session 状态为 Failed，后端返回错误，前端展示错误文案「session status 'Failed' does not accept new messages」
- **无白屏**：页面保持正常渲染

### 5.5 结论

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass** |
| **已验证** | 发送消息流程可执行；错误反馈可见；页面无白屏 |
| **使用的 sessionId** | `b74478cf-1b23-45eb-a60f-26c7ac32d810` |

---

## 6. 前端成功路径补充验证（2026-03-12）

### 6.1 验证目标

补充验证 Session Detail 页面发送消息的**成功路径**（Agent 成功响应，非错误态）。

### 6.2 验证步骤

1) 直接打开 `/sessions/78b1a54e-4855-4445-9c74-cf39dc130055`（由 `POST /api/sre/chat` 创建的可续聊会话）  
2) 在 Send Message 输入「请用一句话继续说明 Playbook 的作用」  
3) 点击发送，验证页面出现 Agent 成功响应文本（非错误文案），并显示 Token 信息  

### 6.3 验证结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开详情页 | 页面可加载 | 页面加载成功，Send Message 区块可见 | Pass |
| 2. 输入并发送 | 可输入、可点击发送 | 输入消息，点击「发送消息」，按钮显示「发送中...」 | Pass |
| 3. Agent 成功响应 | 出现成功响应文本，非错误 | 出现「Agent 响应：成功」及 Agent 输出文本 | Pass |
| 4. Token 信息 | 显示 Token 统计 | 显示「Token: 5279 ( 5203 / 76 )」 | Pass |

### 6.4 关键证据

- **sessionId**：`78b1a54e-4855-4445-9c74-cf39dc130055`
- **Agent 响应**：`Agent 响应：成功`
- **输出文本**：`Playbook 的作用是为工程师提供标准化的故障排查步骤和解决方案，确保在收到告警时能够快速、一致地响应和恢复服务。`
- **Token 信息**：`Token: 5279 ( 5203 / 76 )`

### 6.5 结论

| 项目 | 结果 |
|------|------|
| **前端成功路径补充验证** | **Pass** |
| **已验证** | 可续聊会话发送消息 → Agent 成功响应 → 输出文本与 Token 信息可见 |
