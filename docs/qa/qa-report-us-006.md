# US-006 E2E QA Report: Dashboard API（stats + active-sessions）

**Story**: 作为 SRE 值班工程师，我希望在 Dashboard 首页看到关键统计与活跃会话列表，以便快速判断当前处理压力与重点会话

**验收**: Dashboard 接口 E2E 验证
1) 走一遍链路（trigger-errors -> 等待 alarm -> POST /api/sre/analyze）确保有近期数据；
2) 调用 dashboard 两个接口，校验 200；
3) 校验关键字段：stats（totalSessionsToday/autoResolutionRate/avgProcessingTimeSeconds/pendingApprovals）、active-sessions（items[]/total，items 按 updatedAt 近似降序）；
4) 记录命令、关键响应片段、Pass/Fail 结论。

**执行时间**: 2026-03-12

---

## 执行摘要

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass**（以完整链路为准） |
| **先前降级（2026-03-12 上午）** | ⚠️ trigger 因 AWS SSO 过期未执行；POST /api/sre/analyze 成功，生成近期会话数据 |
| **本次完整链路复测（2026-03-12 下午）** | ✅ trigger → alarm → POST analyze → GET stats → GET active-sessions 全链路通过 |
| **GET /api/dashboard/stats** | ✅ 200，含 totalSessionsToday/autoResolutionRate/avgProcessingTimeSeconds/pendingApprovals |
| **GET /api/dashboard/active-sessions?limit=10** | ✅ 200，含 items[]/total，items 按 updatedAt 降序 |
| **limit 边界校验** | ✅ limit=0 与 limit=51 均返回 400 |

---

## 1. Pre-flight 检查

### 1.1 先前降级执行时（2026-03-12 上午）

| 检查项 | 结果 | 证据 |
|--------|------|------|
| SRE Agent 健康 | ✅ | `curl http://localhost:5099/health` → `{"status":"Healthy"}` |
| AWS 凭证 | ❌ | `aws sts get-caller-identity` → Token has expired and refresh failed |
| trigger-errors | ❌ | Order Service URL 无法获取（依赖 CloudFormation），采用本地降级 |

### 1.2 完整链路复测时（2026-03-12 下午，AWS SSO 恢复后）

| 检查项 | 结果 | 证据 |
|--------|------|------|
| SRE Agent 健康 | ✅ | `curl http://localhost:5099/health` → `{"status":"Healthy"}` |
| AWS 凭证 | ✅ | `aws sts get-caller-identity` → Account 160071257600, Arn: PowerUserPlusRole |
| trigger-errors | ✅ | Order Service URL 可用，3 次请求均返回 500 |

---

## 2. 链路执行

### 2.1 链路步骤

| 步骤 | 命令/动作 | 结果 |
|------|-----------|------|
| 1. Trigger | `./trigger-errors.sh missing-param --count 3` | ❌ Order Service URL not found（AWS 过期） |
| 2. 等待告警 | - | 跳过 |
| 3. POST analyze（降级） | `curl -X POST .../api/sre/analyze -d '{...}'` | ✅ 200, sessionId: `cc4e8c8a-1530-4d9b-ab61-a80e53ccc606` |
| 4. GET stats | `curl http://localhost:5099/api/dashboard/stats` | ✅ 200 |
| 5. GET active-sessions | `curl "http://localhost:5099/api/dashboard/active-sessions?limit=10"` | ✅ 200 |

### 2.2 先前降级关键命令

```bash
# 1. Trigger（本次因 AWS 过期未成功）
cd /Users/baiyunchen/workspace/sre-agent-test-materials
./trigger-errors.sh missing-param --count 3

# 2. POST analyze（本地降级，确保有近期会话数据）
curl -s -X POST http://localhost:5099/api/sre/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "title": "inventory-service-5xx-errors-dev",
    "severity": "P1",
    "alertTime": "2026-03-12T12:00:00Z",
    "affectedService": "inventory-service",
    "description": "Alarm when service returns 5xx errors. Threshold Crossed: datapoint was >= 1.0",
    "additionalInfo": "MetricName: inventory-service-5xx-dev, Namespace: SRETestServices"
  }'

# 3. GET dashboard stats
curl -s http://localhost:5099/api/dashboard/stats

# 4. GET dashboard active-sessions
curl -s "http://localhost:5099/api/dashboard/active-sessions?limit=10"
```

### 2.3 完整链路复测（2026-03-12 下午，AWS SSO 恢复后）

| 步骤 | 命令/动作 | 结果 |
|------|-----------|------|
| 1. Trigger | `./trigger-errors.sh missing-param --count 3` | ✅ 3 次请求均返回 500，Stock lookup failed for key: undefined-PROD001 |
| 2. 等待告警 | `sleep 90` | ✅ |
| 3. 检查告警 | `./trigger-errors.sh alarms` | ✅ inventory-service-log-errors-dev、order-service-log-errors-dev 为 ALARM |
| 4. POST analyze | `curl -X POST .../api/sre/analyze -d '{...}'` | ✅ 200, sessionId: `35883f31-6c3d-4fd0-b2e0-8f5cbda7cd9f`，根因识别 warehouseId 缺失 |
| 5. GET stats | `curl http://localhost:5099/api/dashboard/stats` | ✅ 200 |
| 6. GET active-sessions | `curl "http://localhost:5099/api/dashboard/active-sessions?limit=10"` | ✅ 200 |

**完整链路复测关键命令**：

```bash
cd /Users/baiyunchen/workspace/sre-agent-test-materials
./trigger-errors.sh missing-param --count 3
sleep 90
./trigger-errors.sh alarms

curl -s -X POST http://localhost:5099/api/sre/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "title": "inventory-service-log-errors-dev",
    "severity": "P1",
    "alertTime": "2026-03-12T12:20:41Z",
    "affectedService": "inventory-service",
    "description": "Alarm when log errors occur. Threshold Crossed: datapoint was >= 1.0",
    "additionalInfo": "MetricName: inventory-service-log-errors-dev, Namespace: SRETestServices"
  }'

curl -s http://localhost:5099/api/dashboard/stats
curl -s "http://localhost:5099/api/dashboard/active-sessions?limit=10"
```

---

## 3. 校验结果

### 3.1 GET /api/dashboard/stats

**响应（200）**：

```json
{
  "totalSessionsToday": 2,
  "autoResolutionRate": 50,
  "avgProcessingTimeSeconds": 55,
  "pendingApprovals": 0
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态码 | 200 | 200 | ✅ Pass |
| totalSessionsToday | 存在且为 int | 2 | ✅ Pass |
| autoResolutionRate | 存在且为 number | 50 | ✅ Pass |
| avgProcessingTimeSeconds | 存在且为 int | 55 | ✅ Pass |
| pendingApprovals | 存在且为 int | 0 | ✅ Pass |

### 3.2 GET /api/dashboard/active-sessions?limit=10

**响应（200）**：

```json
{
  "items": [
    {
      "id": "78b1a54e-4855-4445-9c74-cf39dc130055",
      "alertName": null,
      "serviceName": null,
      "status": "Running",
      "currentStep": 1,
      "startedAt": null,
      "updatedAt": "2026-03-12T00:33:35.339409Z"
    },
    {
      "id": "df8f478e-b6c0-4d26-8897-b401cbff42d0",
      "alertName": "inventory-service-5xx-errors-dev",
      "serviceName": "inventory-service",
      "status": "Running",
      "currentStep": 9,
      "startedAt": "2026-03-10T15:42:35.942789Z",
      "updatedAt": "2026-03-10T15:44:38.161189Z"
    }
  ],
  "total": 2
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态码 | 200 | 200 | ✅ Pass |
| items | 数组 | 数组，2 条 | ✅ Pass |
| total | 存在且为 int | 2 | ✅ Pass |
| items 按 updatedAt 降序 | 第一条 updatedAt ≥ 第二条 | 2026-03-12 > 2026-03-10 | ✅ Pass |

### 3.3 limit 边界校验

| 请求 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| limit=1 | 200，items 最多 1 条 | 200，items 1 条，total=2 | ✅ Pass |
| limit=0 | 400 | 400，`{"error":"limit must be between 1 and 50"}` | ✅ Pass |
| limit=51 | 400 | 400，`{"error":"limit must be between 1 and 50"}` | ✅ Pass |

### 3.4 完整链路复测校验（2026-03-12 下午）

**GET /api/dashboard/stats 响应（200）**：

```json
{
  "totalSessionsToday": 3,
  "autoResolutionRate": 66.67,
  "avgProcessingTimeSeconds": 55,
  "pendingApprovals": 0
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态码 | 200 | 200 | ✅ Pass |
| totalSessionsToday | 含本次 analyze 会话 | 3（含 sessionId 35883f31-...） | ✅ Pass |
| 关键字段完整 | totalSessionsToday/autoResolutionRate/avgProcessingTimeSeconds/pendingApprovals | 全部存在 | ✅ Pass |

**GET /api/dashboard/active-sessions?limit=10 响应（200）**：

```json
{
  "items": [
    {"id": "78b1a54e-4855-4445-9c74-cf39dc130055", "alertName": null, "serviceName": null, "status": "Running", "currentStep": 1, "startedAt": null, "updatedAt": "2026-03-12T00:33:35.339409Z"},
    {"id": "df8f478e-b6c0-4d26-8897-b401cbff42d0", "alertName": "inventory-service-5xx-errors-dev", "serviceName": "inventory-service", "status": "Running", "currentStep": 9, "startedAt": "2026-03-10T15:42:35.942789Z", "updatedAt": "2026-03-10T15:44:38.161189Z"}
  ],
  "total": 2
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态码 | 200 | 200 | ✅ Pass |
| items | 数组 | 数组，2 条 | ✅ Pass |
| total | 存在且为 int | 2 | ✅ Pass |
| items 按 updatedAt 降序 | 第一条 updatedAt ≥ 第二条 | 2026-03-12 > 2026-03-10 | ✅ Pass |

**POST /api/sre/analyze 根因校验**：分析结果正确识别 warehouseId 缺失、Order Service 未传递该参数，与 missing-param 场景一致。

---

## 4. 结论

- **US-006 Dashboard API（stats + active-sessions）**：**Pass**
- **先前降级**：因 AWS SSO 过期，trigger 未执行；采用本地降级：POST /api/sre/analyze → GET stats、GET active-sessions，均通过
- **本次完整链路复测（AWS SSO 恢复后）**：trigger missing-param → 等待 alarm → POST /api/sre/analyze → GET /api/dashboard/stats → GET /api/dashboard/active-sessions 全链路通过
- **最终结论**：以完整链路为准，US-006 后端 E2E **Pass**

---

## 5. 风险与后续

| 项目 | 说明 |
|------|------|
| **完整链路复测** | ✅ 已于 2026-03-12 下午完成，AWS SSO 恢复后全链路通过 |

---

## 6. 前端 Dashboard E2E（2026-03-12）

### 6.1 验证目标

1) 打开 `/`（Dashboard）  
2) 验证 4 个统计卡片显示真实值（非 --）  
3) 验证 Active Sessions 区块显示列表或明确空态  
4) 记录 Pass/Fail、关键观察  

### 6.2 验证步骤与结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开 /（Dashboard） | 页面可加载 | 页面加载成功，Dashboard 标题与四块统计卡片可见 | Pass |
| 2. 4 个统计卡片显示真实值 | 非 --，为 API 返回的数值 | Total Sessions Today: 2；Auto-Resolution Rate: 50.0%；Avg Processing Time: 55s；Pending Approvals: 0 | Pass |
| 3. Active Sessions 区块 | 显示列表或明确空态 | 显示 2 条活跃会话：78b1a54e-4855-4445-9c74-cf39dc130055、inventory-service-5xx-errors-dev | Pass |

### 6.3 关键观察

- **4 个统计卡片**：均显示真实值（2、50.0%、55s、0），无 "--" 占位
- **Active Sessions**：显示列表，含 session id/alertName、serviceName、status、currentStep
- **无错误态**：页面无 API 错误提示，无白屏

### 6.4 结论

| 项目 | 结果 |
|------|------|
| **前端 Dashboard E2E** | **Pass** |
| **4 个统计卡片** | 真实值（2、50.0%、55s、0） |
| **Active Sessions** | 列表展示，2 条会话 |
