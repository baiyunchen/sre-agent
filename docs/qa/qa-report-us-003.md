# US-003 E2E QA Report: GET /api/sessions/{id}/timeline 合并时间线

**Story**: 作为 SRE 工程师，我希望在 Session 详情页看到统一时间线，以便追踪完整分析过程

**验收**: 完整链路 trigger → alarm → POST analyze → GET /api/sessions/{id}/timeline，校验 events 为数组、至少两类 eventType、按 timestamp 升序

**执行时间**: 2026-03-11

---

## 执行摘要

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass** |
| **完整链路** | ✅ Pass（trigger → alarm → POST analyze → sessionId 获取） |
| **GET /api/sessions/{id}/timeline** | ✅ 200，events 数组、含 message/agent_run/tool_invocation、按 timestamp 升序 |
| **前置修复** | 重启后端服务加载最新构建，保持契约路由 `/api/sessions/{id}/timeline` |

---

## 1. Pre-flight 检查

| 检查项 | 结果 | 证据 |
|--------|------|------|
| AWS 凭证 | ✅ | `aws sts get-caller-identity` → 有效 |
| SRE Agent 健康 | ✅ | `curl http://localhost:5099/health` → `{"status":"Healthy"}` |
| 测试服务部署 | ✅ | order-service, notification-service, inventory-service 已部署 |

---

## 2. 完整链路执行

### 2.1 链路步骤

| 步骤 | 命令/动作 | 结果 |
|------|-----------|------|
| 1. Trigger | `./trigger-errors.sh missing-param --count 3` | ✅ 3×500，错误：`Stock lookup failed for key: undefined-PROD001` |
| 2. 等待告警 | `sleep 90` + `./trigger-errors.sh alarms` | ✅ inventory-service-log-errors-dev, order-service-log-errors-dev ALARM |
| 3. POST analyze | `curl -X POST .../api/sre/analyze -d '{...}'` | ✅ 200, sessionId: `34649a06-0295-489a-8b48-85544c896888` |
| 4. GET timeline | `curl "http://localhost:5099/api/sessions/{sessionId}/timeline"` | ✅ 200，events 含 message/agent_run/tool_invocation |

### 2.2 关键命令

```bash
# 1. Trigger
cd /Users/baiyunchen/workspace/sre-agent-test-materials
./trigger-errors.sh missing-param --count 3

# 2. Wait & check alarms
sleep 90
./trigger-errors.sh alarms

# 3. POST analyze
curl -s -X POST http://localhost:5099/api/sre/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "title": "inventory-service-log-errors-dev",
    "severity": "P1",
    "alertTime": "2026-03-11T15:40:41.779Z",
    "affectedService": "inventory-service",
    "description": "Alarm when service log errors occur. Threshold Crossed: datapoint was >= 1.0",
    "additionalInfo": "MetricName: inventory-service-log-errors-dev, Namespace: SRETestServices"
  }'

# 4. GET timeline
curl -s "http://localhost:5099/api/sessions/34649a06-0295-489a-8b48-85544c896888/timeline"
```

---

## 3. 校验结果

### 3.1 校验项

| 校验项 | 预期 | 实际 |
|--------|------|------|
| 响应状态码 | 200 | ✅ 200 |
| events 为数组 | 是 | ✅ 是 |
| events 数量 > 0 | 是 | ✅ 是（约 30+ 条） |
| 至少两类 eventType | message/agent_run/tool_invocation 中至少 2 类 | ✅ 三类均有 |
| 按 timestamp 升序 | 是 | ✅ 是（采样验证） |

### 3.2 关键响应片段

```json
{
  "sessionId": "34649a06-0295-489a-8b48-85544c896888",
  "events": [
    {"id": "fb150594-b0f6-40a1-b709-6c103389da6f", "eventType": "message", "timestamp": "2026-03-11T15:41:22.287599Z", "title": "Message: User", "actor": "User"},
    {"id": "5056c871-b32b-474b-838f-ef97feaba711", "eventType": "agent_run", "timestamp": "2026-03-11T15:41:22.505925Z", "title": "Agent Run: SRE 故障分析协调器", "status": "Failed", "actor": "SRE 故障分析协调器"},
    {"id": "7a6c408d-d4c7-47eb-a126-3542099f0957", "eventType": "message", "timestamp": "2026-03-11T15:41:25.708493Z", "title": "Message: Assistant", "actor": "sre-coordinator"},
    {"id": "37848ef6-f9ae-478f-8057-d3bbdfccdce2", "eventType": "tool_invocation", "timestamp": "2026-03-11T15:41:25.710649Z", "title": "Tool: knowledge_base_query", "status": "Completed", "actor": "SRE 故障分析协调器"}
  ]
}
```

### 3.3 timestamp 升序采样

| 序号 | eventType | timestamp |
|------|-----------|-----------|
| 1 | message | 2026-03-11T15:41:22.287599Z |
| 2 | agent_run | 2026-03-11T15:41:22.505925Z |
| 3 | message | 2026-03-11T15:41:25.708493Z |
| 4 | tool_invocation | 2026-03-11T15:41:25.710649Z |

---

## 4. 前置修复说明

执行时首次出现 `GET /api/sessions/{id}/timeline` 返回 404。复核后确认为运行进程未加载最新构建；重启后端并保持契约路由 `/api/sessions/{id}/timeline` 后，接口恢复正常返回 200。

---

## 5. 结论

- **US-003 GET /api/sessions/{id}/timeline 合并时间线**：**Pass**
- **链路**：trigger-errors missing-param → CloudWatch alarm → POST /api/sre/analyze → GET /api/sessions/{id}/timeline
- **证据**：响应 200，events 数组含 message、agent_run、tool_invocation 三类，按 timestamp 升序

---

## 6. 前端最小回归（2026-03-11）

### 6.1 验证目标

US-003 主要为后端 timeline API，前端当前仅做回归验证。
1) 打开 /sessions，确认页面可加载  
2) 点击第一条会话进入 /sessions/:id，确认详情页可加载  
3) 记录关键观察与结论（Pass/Fail）。

### 6.2 验证步骤与结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开 /sessions | 页面可加载 | 页面加载成功，筛选输入与表头可见，列表加载成功 | Pass |
| 2. 点击第一条进入 /sessions/:id | 详情页可加载 | 点击首条链接后跳转至 `/sessions/34649a06-0295-489a-8b48-85544c896888`，Session Detail、Timeline、Diagnosis、Tool Invocations、Todos 区块可见 | Pass |

### 6.3 关键观察

- **列表数据**：可见 inventory-service-log-errors-dev、inventory-service-5xx-errors-dev 等会话链接
- **详情页**：`Session ID: 34649a06-0295-489a-8b48-85544c896888`，Timeline、Diagnosis、Tool Invocations、Todos 均可见
- **无崩溃**：页面无白屏、无控制台 error

### 6.4 结论

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass** |
| **已验证** | /sessions 列表加载、点击首条进入详情、详情页加载 |
