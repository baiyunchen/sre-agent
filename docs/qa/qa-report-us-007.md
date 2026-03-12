# US-007 E2E QA Report: Dashboard Activities API

**Story**: 作为 SRE 值班工程师，我希望在 Dashboard 查看最近活动流，以便快速了解系统在不同会话上的最新动作

**验收**: `GET /api/dashboard/activities` 接口 E2E 验证
1) 触发完整链路（trigger-errors missing-param -> 等待 alarms -> POST /api/sre/analyze）产生新审计活动；
2) 校验 `GET /api/dashboard/activities?limit=20` 返回 200；
3) 校验 `items[]/total`，且 items 按 occurredAt 降序；
4) 至少验证 limit 边界：0 与 101 返回 400；
5) 在报告里记录命令、关键响应、结论。

**执行时间**: 2026-03-12

---

## 执行摘要

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass** |
| **完整链路** | ✅ trigger missing-param → 等待 alarm → POST /api/sre/analyze 成功 |
| **GET /api/dashboard/activities?limit=20** | ✅ 200，含 items[]/total，items 按 occurredAt 降序 |
| **limit 边界校验** | ✅ limit=0 与 limit=101 均返回 400 |

---

## 1. Pre-flight 检查

| 检查项 | 结果 | 证据 |
|--------|------|------|
| SRE Agent 健康 | ✅ | `curl http://localhost:5099/health` → `{"status":"Healthy"}` |
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
| 4. POST analyze | `curl -X POST .../api/sre/analyze -d '{...}'` | ✅ 200, sessionId: `1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78` |
| 5. GET activities | `curl "http://localhost:5099/api/dashboard/activities?limit=20"` | ✅ 200 |

### 2.2 关键命令

```bash
# 1. Trigger
cd /Users/baiyunchen/workspace/sre-agent-test-materials
./trigger-errors.sh missing-param --count 3

# 2. 等待告警
sleep 90

# 3. 检查告警
./trigger-errors.sh alarms

# 4. POST analyze（产生新审计活动）
curl -s -X POST http://localhost:5099/api/sre/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "title": "inventory-service-log-errors-dev",
    "severity": "P1",
    "alertTime": "2026-03-12T12:30:41Z",
    "affectedService": "inventory-service",
    "description": "Alarm when log errors occur. Threshold Crossed: datapoint was >= 1.0",
    "additionalInfo": "MetricName: inventory-service-log-errors-dev, Namespace: SRETestServices"
  }'

# 5. GET activities
curl -s "http://localhost:5099/api/dashboard/activities?limit=20"
```

---

## 3. 校验结果

### 3.1 GET /api/dashboard/activities?limit=20

**响应（200）**：

```json
{
  "items": [
    {
      "id": "5e49f90b-bb1a-4e5a-a9a1-99252dc47cd7",
      "sessionId": "1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78",
      "eventType": "SessionFailed",
      "description": "Analysis failed: Reached maximum iterations",
      "actor": "system",
      "occurredAt": "2026-03-12T12:32:29.889172Z"
    },
    {
      "id": "680d4cdf-9515-4c54-8fcd-6cac3760b64a",
      "sessionId": "1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78",
      "eventType": "SessionStarted",
      "description": "Analysis session started for alert: inventory-service-log-errors-dev",
      "actor": "system",
      "occurredAt": "2026-03-12T12:31:32.088896Z"
    },
    ...
  ],
  "total": 32
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态码 | 200 | 200 | ✅ Pass |
| items | 数组 | 数组，最多 20 条 | ✅ Pass |
| total | 存在且为 int | 32 | ✅ Pass |
| items 按 occurredAt 降序 | 第一条 occurredAt ≥ 第二条 | 12:32:29 > 12:31:32 | ✅ Pass |
| 新会话活动可见 | 本次 analyze 的 sessionId 在列表中 | 1ce8d083-... 的 SessionStarted/SessionFailed 在首条附近 | ✅ Pass |

### 3.2 limit 边界校验

| 请求 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| limit=20 | 200，items 最多 20 条 | 200，items 20 条，total=32 | ✅ Pass |
| limit=0 | 400 | 400，`{"error":"limit must be between 1 and 100"}` | ✅ Pass |
| limit=101 | 400 | 400，`{"error":"limit must be between 1 and 100"}` | ✅ Pass |

---

## 4. 结论

- **US-007 Dashboard Activities API**：**Pass**
- **完整链路**：trigger missing-param → 等待 alarm → POST /api/sre/analyze 成功，产生新审计活动
- **GET /api/dashboard/activities?limit=20**：200，含 items[]/total，items 按 occurredAt 降序
- **limit 边界**：limit=0 与 limit=101 均正确返回 400

---

## 5. 风险与后续

| 项目 | 说明 |
|------|------|
| **后端重启** | 新端点发布后需确保 API 进程已重启并加载最新构建 |
| **后续范围** | `backend-api-p1` 仍有 `dashboard SSE/approval CRUD/tools-agents registry` 待实现 |

---

## 6. 前端 Dashboard E2E（2026-03-12）

### 6.1 验证目标

1) 打开 `/` Dashboard  
2) 验证 Recent Activities 区块可见  
3) 若有数据，至少看到 1 条 eventType + 时间；若无数据，看到明确空态文案  
4) 记录 Pass/Fail 与关键观察  

### 6.2 验证步骤与结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开 /（Dashboard） | 页面可加载 | 页面加载成功 | Pass |
| 2. Recent Activities 区块可见 | 区块存在 | heading「Recent Activities」可见 | Pass |
| 3. 数据或空态 | 有数据则至少 1 条 eventType+时间；无数据则明确空态 | 有数据，可见 SessionFailed、SessionStarted、SessionCompleted、SessionMessageProcessed、SessionMessageSent 等 eventType，每条含时间（如 3/12/2026, 8:32:29 PM） | Pass |

### 6.3 关键观察

- **Recent Activities 区块**：可见，展示活动流列表
- **eventType**：SessionFailed、SessionStarted、SessionCompleted、SessionMessageProcessed、SessionMessageSent 等
- **时间**：每条活动含 occurredAt 格式化时间（如 3/12/2026, 8:32:29 PM）
- **描述**：含 description、actor（如 system、user）

### 6.4 结论

| 项目 | 结果 |
|------|------|
| **前端 Dashboard Recent Activities E2E** | **Pass** |
| **Recent Activities** | 区块可见，有数据，eventType + 时间均可见 |
