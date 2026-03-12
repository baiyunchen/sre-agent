# US-008 E2E QA Report: Dashboard SSE Stream

**Story**: 作为 SRE 值班工程师，我希望 Dashboard 能实时刷新关键信息，以便无需手动刷新即可及时感知会话变化

**验收**: `GET /api/events/stream` SSE 端点与 `dashboard.snapshot` 事件 E2E 验证
1) 按完整链路触发数据：`trigger-errors missing-param --count 3` -> 等待 alarm -> `POST /api/sre/analyze`；
2) 使用 `curl -N --max-time` 订阅 `GET /api/events/stream`，验证至少收到 1 条 `event: dashboard.snapshot` 与 `data:` JSON；
3) 校验 JSON 至少包含：`eventType/generatedAt/stats/activeSessions/activities`；
4) 记录命令、关键输出片段、Pass/Fail 结论；
5) 如有失败重试并记录。

**执行时间**: 2026-03-12

---

## 执行摘要

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass** |
| **完整链路** | ✅ trigger missing-param → 等待 alarm → POST /api/sre/analyze 成功 |
| **SSE 订阅** | ✅ `curl -N --max-time 25` 成功连接并接收事件 |
| **dashboard.snapshot** | ✅ 至少收到 3 条，含 `event:` 与 `data:` JSON |
| **JSON 字段校验** | ✅ eventType/generatedAt/stats/activeSessions/activities 均存在 |

---

## 1. Pre-flight 检查

| 检查项 | 结果 | 证据 |
|--------|------|------|
| SRE Agent 健康 | ✅ | `curl http://localhost:5099/health` → `{"status":"Healthy","timestamp":"2026-03-12T13:01:48.891824Z"}` |
| AWS 凭证 | ✅ | `aws sts get-caller-identity` → Account 160071257600 |
| trigger-errors | ✅ | Order Service URL 可用，3 次请求均返回 500 |

---

## 2. 链路执行

### 2.1 链路步骤

| 步骤 | 命令/动作 | 结果 |
|------|-----------|------|
| 1. Trigger | `./trigger-errors.sh missing-param --count 3` | ✅ 3 次请求均返回 500，Stock lookup failed for key: undefined-PROD001 |
| 2. 等待告警 | `sleep 90` | ✅ |
| 3. 检查告警 | `./trigger-errors.sh alarms` | ✅ inventory-service-log-errors-dev、order-service-log-errors-dev 为 ALARM |
| 4. SSE 订阅 | `curl -N --max-time 25` 订阅 `/api/events/stream` | ✅ 收到 3 条 `event: dashboard.snapshot` |
| 5. POST analyze | `curl -X POST .../api/sre/analyze -d '{...}'` | ✅ 200, sessionId: `c01dd223-de40-4c68-8f24-2e7e3fe2c91d` |

### 2.2 关键命令

```bash
# 1. Trigger
cd /Users/baiyunchen/workspace/sre-agent-test-materials
./trigger-errors.sh missing-param --count 3

# 2. 等待告警
sleep 90

# 3. 检查告警
./trigger-errors.sh alarms

# 4. SSE 订阅（验证 dashboard.snapshot 事件）
curl -s -N --max-time 25 "http://localhost:5099/api/events/stream" 2>&1 | tee /tmp/sse-us008-output.txt

# 5. POST analyze（产生新会话与审计活动）
curl -s -X POST http://localhost:5099/api/sre/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "title": "inventory-service-log-errors-dev",
    "severity": "P1",
    "alertTime": "2026-03-12T13:02:41Z",
    "affectedService": "inventory-service",
    "description": "Alarm when log errors occur. Threshold Crossed: datapoint was >= 1.0",
    "additionalInfo": "MetricName: inventory-service-log-errors-dev, Namespace: SRETestServices"
  }'
```

---

## 3. SSE 校验结果

### 3.1 事件格式

**收到事件示例**（第 1 条）：

```
event: dashboard.snapshot
data: {"eventType":"dashboard.snapshot","generatedAt":"2026-03-12T13:03:48.773163Z","stats":{"totalSessionsToday":4,"autoResolutionRate":50,"avgProcessingTimeSeconds":56,"pendingApprovals":0},"activeSessions":{"items":[...],"total":2},"activities":{"items":[...],"total":32}}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| `event:` 行 | `event: dashboard.snapshot` | 存在 | ✅ Pass |
| `data:` 行 | JSON 字符串 | 存在 | ✅ Pass |
| 事件数量 | ≥ 1 | 3 条 | ✅ Pass |
| 推送间隔 | ~10s | 约 10s、20s 各推送 | ✅ Pass |

### 3.2 JSON 字段校验

| 字段 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| `eventType` | 存在 | `"dashboard.snapshot"` | ✅ Pass |
| `generatedAt` | 存在，ISO 8601 | `"2026-03-12T13:03:48.773163Z"` | ✅ Pass |
| `stats` | 存在，对象 | `totalSessionsToday`, `autoResolutionRate`, `avgProcessingTimeSeconds`, `pendingApprovals` | ✅ Pass |
| `activeSessions` | 存在，含 items/total | `items: [...]`, `total: 2` | ✅ Pass |
| `activities` | 存在，含 items/total | `items: [...]`, `total: 32` | ✅ Pass |

### 3.3 完整 JSON 片段（截取）

```json
{
  "eventType": "dashboard.snapshot",
  "generatedAt": "2026-03-12T13:03:48.773163Z",
  "stats": {
    "totalSessionsToday": 4,
    "autoResolutionRate": 50,
    "avgProcessingTimeSeconds": 56,
    "pendingApprovals": 0
  },
  "activeSessions": {
    "items": [
      {
        "id": "78b1a54e-4855-4445-9c74-cf39dc130055",
        "status": "Running",
        "currentStep": 1,
        ...
      }
    ],
    "total": 2
  },
  "activities": {
    "items": [
      {
        "id": "5e49f90b-bb1a-4e5a-a9a1-99252dc47cd7",
        "sessionId": "1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78",
        "eventType": "SessionFailed",
        "description": "Analysis failed: Reached maximum iterations",
        "actor": "system",
        "occurredAt": "2026-03-12T12:32:29.889172Z"
      },
      ...
    ],
    "total": 32
  }
}
```

---

## 4. 结论

- **US-008 Dashboard SSE Stream**：**Pass**
- **完整链路**：trigger missing-param → 等待 alarm → POST /api/sre/analyze 成功
- **SSE 端点**：`GET /api/events/stream` 连接正常，持续推送 `dashboard.snapshot` 事件
- **JSON 结构**：`eventType`、`generatedAt`、`stats`、`activeSessions`、`activities` 均存在且符合契约

---

## 5. 重试记录

无失败，本次执行无需重试。

---

## 6. 风险与后续

| 项目 | 说明 |
|------|------|
| **curl 超时** | `--max-time 25` 会以 exit code 28 结束，属预期行为 |
| **SSE 推送间隔** | 当前为 10 秒，可配置 `StreamPushInterval` |
| **后续范围** | `backend-api-p1` 仍有 approval CRUD / tools-agents registry 待实现 |

---

## 7. 前端 Dashboard SSE E2E（2026-03-12）

### 7.1 验证目标

1) 打开 `/` Dashboard  
2) 验证页面显示实时通道状态文案（连接中/已连接/重连中）  
3) 在页面停留一段时间后，验证「最后更新」时间出现或刷新（表示收到 SSE 事件）  
4) 记录 Pass/Fail 与关键观察  

### 7.2 验证步骤与结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开 /（Dashboard） | 页面可加载 | 页面加载成功 | Pass |
| 2. 实时通道状态文案 | 显示连接中/已连接/重连中 | 初始显示「实时通道: 连接中」，约 15s 后变为「实时通道: 已连接」 | Pass |
| 3. 最后更新时间出现 | 收到 SSE 后显示「最后更新」+ 时间 | 显示「实时通道: 已连接 · 最后更新 3/12/2026, 9:06:14 PM」 | Pass |
| 4. 最后更新时间刷新 | 持续收到 SSE 后时间更新 | 约 12s 后刷新为「最后更新 3/12/2026, 9:06:34 PM」 | Pass |

### 7.3 关键观察

- **状态流转**：连接中 → 已连接（本次未观察到重连中）
- **最后更新**：首次约 15s 内出现，之后约每 10s 刷新一次，与 SSE 推送间隔一致
- **无错误**：页面无 SSE 连接错误提示

### 7.4 结论

| 项目 | 结果 |
|------|------|
| **前端 Dashboard SSE E2E** | **Pass** |
| **实时通道状态** | 连接中/已连接 可见 |
| **最后更新** | 出现且随 SSE 事件刷新 |
