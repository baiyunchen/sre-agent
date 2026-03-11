# US-002 E2E QA Report: GET /api/sessions/{id} 返回 source/severity/duration/agentSteps

**Story**: 验证新接口 GET /api/sessions/{id} 能返回 source、severity、duration、agentSteps

**验收**: 完整链路 → 从 analyze 获取 sessionId → GET 详情校验字段 → GET 列表校验可见

**执行时间**: 2026-03-11

---

## 执行摘要

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Fail** |
| **完整链路** | ✅ Pass（trigger → alarm → POST analyze → sessionId 获取） |
| **GET /api/sessions/{id}** | ⚠️ 路由 /api/sessions/{id} 返回 404；/api/Session/{id} 返回 200 但响应缺少 source/severity/duration/agentSteps |
| **GET /api/sessions 列表** | ✅ Pass（session 可见，但 source=null） |
| **字段校验** | ❌ source 为空（要求非空）；severity/duration/agentSteps 在列表中为数值 |

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
| 3. POST analyze | `curl -X POST .../api/sre/analyze -d '{...}'` | ✅ 200, sessionId: `eddb3bd9-4ba1-4f27-858d-bcb7ed8ab57f` |
| 4. GET session detail | `curl "http://localhost:5099/api/sessions/{sessionId}"` | ❌ 404 |
| 4b. GET session detail (alt route) | `curl "http://localhost:5099/api/Session/{sessionId}"` | ✅ 200，但响应缺少 source/severity/duration/agentSteps |
| 5. GET sessions list | `curl "http://localhost:5099/api/sessions?page=1&pageSize=5"` | ✅ session 在列表中，source=null, severity=P1, duration=54, agentSteps=0 |

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
    "title": "inventory-service-5xx-errors-dev",
    "severity": "P1",
    "alertTime": "2026-03-11T15:14:29Z",
    "affectedService": "inventory-service",
    "description": "Alarm when service returns 5xx errors. Threshold Crossed: datapoint was >= 1.0",
    "additionalInfo": "MetricName: inventory-service-5xx-dev, Namespace: SRETestServices"
  }'

# 4. GET session detail (contract path)
curl -s -w "\nHTTP:%{http_code}" "http://localhost:5099/api/sessions/eddb3bd9-4ba1-4f27-858d-bcb7ed8ab57f"

# 4b. GET session detail (alternate route - 200)
curl -s "http://localhost:5099/api/Session/eddb3bd9-4ba1-4f27-858d-bcb7ed8ab57f"

# 5. GET sessions list
curl -s "http://localhost:5099/api/sessions?page=1&pageSize=5"
```

---

## 3. 关键响应片段

### 3.1 POST /api/sre/analyze 响应

```json
{
  "sessionId": "eddb3bd9-4ba1-4f27-858d-bcb7ed8ab57f",
  "success": false,
  "analysis": null,
  "error": "Reached maximum iterations",
  "tasks": [...],
  "tokenUsage": {"promptTokens":184916,"completionTokens":2114,"totalTokens":187030},
  "iterationCount": 0
}
```

### 3.2 GET /api/sessions/{id} 响应（/api/sessions/{id} → 404）

```
HTTP_STATUS:404
```

### 3.3 GET /api/Session/{id} 响应（/api/Session/{id} → 200，缺少目标字段）

```json
{
  "id": "eddb3bd9-4ba1-4f27-858d-bcb7ed8ab57f",
  "status": "Failed",
  "alertId": "alert-eddb3bd94ba14f27858dbcb7ed8ab5",
  "alertName": "inventory-service-5xx-errors-dev",
  "currentAgentId": "sre-coordinator",
  "currentStep": 0,
  "diagnosisSummary": "",
  "confidence": null,
  "createdAt": "2026-03-11T15:14:29.950129Z",
  "startedAt": "2026-03-11T15:14:29.949422Z",
  "completedAt": "2026-03-11T15:15:24.524225Z",
  "updatedAt": "2026-03-11T15:15:24.526403Z"
}
```

**缺失字段**: `source`, `severity`, `duration`, `agentSteps`, `serviceName`（均为契约要求）

### 3.4 GET /api/sessions 列表（session 可见）

```json
{
  "items": [
    {
      "id": "eddb3bd9-4ba1-4f27-858d-bcb7ed8ab57f",
      "status": "Failed",
      "alertName": "inventory-service-5xx-errors-dev",
      "serviceName": "inventory-service",
      "source": null,
      "severity": "P1",
      "duration": 54,
      "agentSteps": 0,
      "createdAt": "2026-03-11T15:14:29.950129Z",
      "updatedAt": "2026-03-11T15:15:24.526403Z"
    },
    ...
  ],
  "total": 4,
  "page": 1,
  "pageSize": 5
}
```

---

## 4. 字段校验结果

| 字段 | 要求 | 列表中的值 | 详情中的值 | 结论 |
|------|------|------------|------------|------|
| source | 存在且非空 | null | 缺失 | ❌ Fail |
| severity | 存在且非空 | "P1" | 缺失 | ⚠️ 列表有，详情缺失 |
| duration | 存在且为数值 | 54 | 缺失 | ⚠️ 列表有，详情缺失 |
| agentSteps | 存在且为数值 | 0 | 缺失 | ⚠️ 列表有，详情缺失 |

---

## 5. 问题分析

1. **路由不一致**: 契约路径 `/api/sessions/{sessionId}` 返回 404；实际可用路径为 `/api/Session/{sessionId}`（controller 名 `Session` 导致）。
2. **source 为空**: DB 中 `alert_source` 列为空，PostgresContextStore 的 `alert_source` 未正确持久化或 BuildAlertMetadata 未正确传递。
3. **详情响应缺字段**: GET /api/Session/{id} 返回的 JSON 中未包含 source、severity、duration、agentSteps，可能因 null 被 JSON 序列化省略，或 MapSessionDetail 未正确填充。

---

## 6. 结论

- **US-002 GET /api/sessions/{id} 返回 source/severity/duration/agentSteps**：**Fail**
- **阻塞点**:
  1. `source` 为空，违反「非空」要求
  2. 契约路径 `/api/sessions/{id}` 返回 404
  3. 详情接口响应缺少 source、severity、duration、agentSteps 字段（列表中有 duration/agentSteps/severity，但 source 为 null）

---

## 7. 建议修复

1. 统一路由：确保 `/api/sessions/{sessionId}` 可访问（与契约一致）。
2. 修复 `alert_source` 持久化：确保 BuildAlertMetadata 中的 `alert_source`（如 "CloudWatch"）正确写入 Session 表。
3. 确保 SessionDetailResponse 序列化时包含 source、severity、duration、agentSteps（包括 null 或 0 时也输出）。

---

## 8. 重跑结果（2026-03-11）

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass** |
| **完整链路** | ✅ trigger missing-param → alarm → POST analyze → sessionId |
| **GET /api/sessions/{id}** | ✅ 200，source/severity/duration/agentSteps 均存在且非空 |
| **GET /api/sessions 列表** | ✅ session 在列表中，source="CloudWatch", severity="Critical" |
| **字段校验** | ✅ source="CloudWatch", severity="Critical", duration=62, agentSteps=0 |

### 重跑证据

- **sessionId**: `0e909fba-391a-498b-a753-66fe7251ac72`
- **GET /api/sessions/{id} 响应**:
```json
{"id":"0e909fba-391a-498b-a753-66fe7251ac72","status":"Failed","alertId":"alert-0e909fba391a498ba75366fe7251ac","alertName":"inventory-service-5xx-errors-dev","source":"CloudWatch","severity":"Critical","serviceName":"inventory-service","currentAgentId":"sre-coordinator","currentStep":0,"agentSteps":0,"diagnosisSummary":"","confidence":null,"duration":62,"createdAt":"2026-03-11T15:20:19.821637Z","startedAt":"2026-03-11T15:20:19.820238Z","completedAt":"2026-03-11T15:21:22.719799Z","updatedAt":"2026-03-11T15:21:22.722519Z"}
```
- **GET /api/sessions?page=1&pageSize=5**：该 session 位于列表首条，source/severity/duration/agentSteps 均正确。

---

## 9. 前端 E2E 回归（2026-03-11）

### 9.1 验证目标

1) 打开 `/sessions`，确认页面可加载、不崩溃  
2) 若后端接口可访问，确认列表有数据行，且至少一个会话显示 source 与 duration/steps 列  
3) 点击第一条会话进入 `/sessions/:id`，确认详情页面可加载  
4) 记录预期/实际和最终 Pass/Fail  

### 9.2 执行环境

| 项目 | 值 |
|------|-----|
| 前端地址 | http://localhost:4173 |
| 后端地址 | http://localhost:5099 |
| 后端健康 | ✅ `curl http://localhost:5099/api/sessions?page=1&pageSize=5` → 200 |

### 9.3 验证步骤与结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开 /sessions | 页面可加载，不崩溃 | 页面加载成功，筛选输入与表头可见 | Pass |
| 2. 列表数据与列 | 若接口可访问：有数据行，至少一行显示 source、duration/steps | CORS 阻断：`http://localhost:5099` 未返回 `Access-Control-Allow-Origin`，fetch 被拦截 | N/A（见风险） |
| 3. 错误态展示 | 若 CORS：页面展示错误态且无崩溃 | 页面保持渲染，表格展示错误态（fetch 失败），无白屏/崩溃 | Pass |
| 4. 详情页加载 | 点击首条会话或直接访问 `/sessions/:id` 可加载 | 直接访问 `/sessions/0e909fba-391a-498b-a753-66fe7251ac72` 成功，Session Detail、Timeline、Diagnosis、Tool Invocations、Todos 区块可见 | Pass |

### 9.4 关键证据

- **CORS 控制台**：`Access to fetch at 'http://localhost:5099/api/sessions?...' from origin 'http://localhost:4173' has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header`
- **详情页快照**：`Session ID: 0e909fba-391a-498b-a753-66fe7251ac72`、Timeline、Diagnosis、Tool Invocations、Todos 均可见

### 9.5 结论

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **部分通过（Partial Pass）** |
| **风险** | CORS 阻断：前端 4173 访问后端 5099 被拦截，无法验证列表数据与 source/duration/steps 列展示；需后端放开 `http://localhost:4173` 或使用 Vite 代理 |
| **已通过项** | /sessions 页面可加载、不崩溃；错误态展示正常；/sessions/:id 详情页可加载 |
| **未验证项** | 列表数据行、source/duration/steps 列展示、从列表点击进入详情 |

---

### 9.6 重跑后结果（2026-03-11）

#### 9.6.1 验证步骤与结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开 /sessions，列表加载后端数据 | 不再 CORS，列表有数据 | 列表成功加载，可见多条会话（inventory-service-5xx-errors-dev、inventory-service-log-errors-dev 等） | Pass |
| 2. 第一条记录 source/duration/steps | 至少一行可见 source、duration、steps 列值 | 首条 `0e909fba-391a-498b-a753-66fe7251ac72`：source=CloudWatch, duration=62, agentSteps=0（API 与 UI 一致） | Pass |
| 3. 点击第一条进入 /sessions/:id | 详情页可加载 | 点击首条链接后跳转至 `/sessions/0e909fba-391a-498b-a753-66fe7251ac72`，Session Detail、Timeline、Diagnosis、Tool Invocations、Todos 区块可见 | Pass |
| 4. CORS 状态 | 无 CORS 报错 | 本次重跑无 CORS 阻断，列表数据正常加载 | Pass |

#### 9.6.2 关键证据

- **列表数据**：页面快照可见 `inventory-service-5xx-errors-dev`、`inventory-service-log-errors-dev` 等会话链接
- **首条记录 API**：`GET /api/sessions?page=1&pageSize=5` 首条：`source:"CloudWatch"`, `duration:62`, `agentSteps:0`
- **详情页**：`Session ID: 0e909fba-391a-498b-a753-66fe7251ac72`，Timeline、Diagnosis、Tool Invocations、Todos 均可见
- **CORS**：后端已配置 `WithOrigins("http://localhost:4173")`，本次无 CORS 报错

#### 9.6.3 重跑结论

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass** |
| **CORS** | 已解决，无当前报错 |
| **已验证** | 列表加载、source/duration/steps 列、点击进入详情、详情页加载 |
