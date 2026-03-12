# US-009 E2E QA Report: Approvals 待审批与审批历史

**Story**: 作为审批人，我希望在 Approvals 页面处理待审批项并查看审批历史，以便及时决策并可追溯

**验收**: Approvals API 全链路 E2E 验证
1) 准备至少 1 个 WaitingApproval 会话（若无可先 POST analyze，再通过数据库将该 session 调整为 WaitingApproval）；
2) 校验 pending 返回该会话；
3) 对一个会话执行 approve，验证状态变化并进入 history；
4) 对另一个会话执行 reject，验证状态变化并进入 history；
5) 校验错误分支（如 approverId 为空返回 400）；
6) 记录命令、关键响应片段、Pass/Fail 结论。

**执行时间**: 2026-03-12

---

## 执行摘要

| 项目 | 结果 |
|------|------|
| **最终 verdict** | **Pass** |
| **数据准备** | ✅ 通过 DB 将 2 个已有会话调整为 WaitingApproval |
| **GET pending** | ✅ 返回 2 个待审批会话 |
| **POST approve** | ✅ 状态变为 Running，记录进入 history |
| **POST reject** | ✅ 状态变为 Cancelled，记录进入 history |
| **错误分支** | ✅ approverId 空/空白返回 400；404/400 符合预期 |

---

## 1. Pre-flight 检查

| 检查项 | 结果 | 证据 |
|--------|------|------|
| SRE Agent 健康 | ✅ | `curl http://localhost:5099/health` → `{"status":"Healthy","timestamp":"..."}` |
| 数据库连接 | ✅ | `psql` 可查询 `sessions` 表 |

---

## 2. 数据准备

### 2.1 操作说明

系统内已有会话（来自此前 analyze 调用），但无 WaitingApproval 状态。采用**直接更新数据库**方式将 2 个会话调整为 WaitingApproval，用于 approve/reject 各一条用例。

**若需从零准备**：可先 `POST /api/sre/analyze` 创建会话，再执行下述 SQL 将对应 `id` 的 `status` 更新为 `WaitingApproval`。

### 2.2 执行的 SQL

```sql
UPDATE sessions SET status = 'WaitingApproval', updated_at = NOW()
WHERE id IN (
  'c01dd223-de40-4c68-8f24-2e7e3fe2c91d',
  '1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78'
);
```

**执行结果：** `UPDATE 2`，2 行会话已更新为 WaitingApproval。

### 2.3 验证

```bash
PGPASSWORD=sre_agent psql -h localhost -p 5432 -U sre_agent -d sre_agent -t -c \
  "SELECT id, status, alert_name FROM sessions WHERE status = 'WaitingApproval';"
```

| id | status | alert_name |
|----|--------|------------|
| 1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78 | WaitingApproval | inventory-service-log-errors-dev |
| c01dd223-de40-4c68-8f24-2e7e3fe2c91d | WaitingApproval | inventory-service-log-errors-dev |

---

## 3. 接口测试

### 3.1 GET /api/approvals/pending

**命令：**

```bash
curl -s "http://localhost:5099/api/approvals/pending?limit=20"
```

**响应：**

```json
{
  "items": [
    {
      "sessionId": "c01dd223-de40-4c68-8f24-2e7e3fe2c91d",
      "alertName": "inventory-service-log-errors-dev",
      "serviceName": "inventory-service",
      "status": "WaitingApproval",
      "updatedAt": "2026-03-12T13:15:22.77039Z"
    },
    {
      "sessionId": "1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78",
      "alertName": "inventory-service-log-errors-dev",
      "serviceName": "inventory-service",
      "status": "WaitingApproval",
      "updatedAt": "2026-03-12T13:15:22.77039Z"
    }
  ],
  "total": 2
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态 | 200 | 200 | ✅ Pass |
| items 含 WaitingApproval 会话 | 是 | 2 条 | ✅ Pass |
| total 与 items 一致 | 是 | total=2 | ✅ Pass |

---

### 3.2 POST /api/approvals/{sessionId}/approve

**命令：**

```bash
curl -s -X POST "http://localhost:5099/api/approvals/c01dd223-de40-4c68-8f24-2e7e3fe2c91d/approve" \
  -H "Content-Type: application/json" \
  -d '{"approverId":"e2e-approver-001","comment":"US-009 E2E approve test"}'
```

**响应：**

```json
{
  "sessionId": "c01dd223-de40-4c68-8f24-2e7e3fe2c91d",
  "status": "Running",
  "message": "Approval accepted"
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态 | 200 | 200 | ✅ Pass |
| status 变为 Running | 是 | `"Running"` | ✅ Pass |
| message 为 Approval accepted | 是 | 是 | ✅ Pass |

**后续验证：**

- `GET /api/approvals/pending`：该会话不再出现在 pending 中（total=1）
- `GET /api/approvals/history`：含 approve 记录，`action: "Approve"`，`intervenedBy: "e2e-approver-001"`
- DB：`sessions.status = 'Running'`

---

### 3.3 POST /api/approvals/{sessionId}/reject

**命令：**

```bash
curl -s -X POST "http://localhost:5099/api/approvals/1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78/reject" \
  -H "Content-Type: application/json" \
  -d '{"approverId":"e2e-approver-002","comment":"US-009 E2E reject test"}'
```

**响应：**

```json
{
  "sessionId": "1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78",
  "status": "Cancelled",
  "message": "Rejection accepted"
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态 | 200 | 200 | ✅ Pass |
| status 变为 Cancelled | 是 | `"Cancelled"` | ✅ Pass |
| message 为 Rejection accepted | 是 | 是 | ✅ Pass |

**后续验证：**

- `GET /api/approvals/pending`：该会话不再出现（total=0）
- `GET /api/approvals/history`：含 reject 记录，`action: "Reject"`，`intervenedBy: "e2e-approver-002"`
- DB：`sessions.status = 'Cancelled'`

---

### 3.4 GET /api/approvals/history

**命令：**

```bash
curl -s "http://localhost:5099/api/approvals/history?limit=10"
```

**响应（approve/reject 后）：**

```json
{
  "items": [
    {
      "id": "b6fd4e96-eb8f-471e-afa6-aa75078be39c",
      "sessionId": "1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78",
      "action": "Reject",
      "reason": "US-009 E2E reject test",
      "intervenedBy": "e2e-approver-002",
      "intervenedAt": "2026-03-12T13:15:37.2594Z"
    },
    {
      "id": "73785b47-7226-4391-85d9-726a54198bfb",
      "sessionId": "c01dd223-de40-4c68-8f24-2e7e3fe2c91d",
      "action": "Approve",
      "reason": "US-009 E2E approve test",
      "intervenedBy": "e2e-approver-001",
      "intervenedAt": "2026-03-12T13:15:29.251344Z"
    }
  ],
  "total": 2
}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态 | 200 | 200 | ✅ Pass |
| items 含 approve/reject 记录 | 是 | 2 条 | ✅ Pass |
| action 为 Approve/Reject | 是 | 是 | ✅ Pass |
| intervenedBy 正确 | 是 | 是 | ✅ Pass |
| 按时间降序 | 是 | Reject 在前（后执行） | ✅ Pass |

---

## 4. 错误分支

### 4.1 approverId 为空

**命令：**

```bash
curl -s -X POST "http://localhost:5099/api/approvals/c01dd223-de40-4c68-8f24-2e7e3fe2c91d/approve" \
  -H "Content-Type: application/json" \
  -d '{"approverId":"","comment":"test"}'
```

**响应：** HTTP 400

```json
{"error":"approverId cannot be empty"}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态 | 400 | 400 | ✅ Pass |
| 错误信息 | approverId cannot be empty | 是 | ✅ Pass |

### 4.2 approverId 为空白

**命令：**

```bash
curl -s -X POST "http://localhost:5099/api/approvals/c01dd223-de40-4c68-8f24-2e7e3fe2c91d/reject" \
  -H "Content-Type: application/json" \
  -d '{"approverId":"   ","comment":"test"}'
```

**响应：** HTTP 400

```json
{"error":"approverId cannot be empty"}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态 | 400 | 400 | ✅ Pass |

### 4.3 会话不存在

**命令：**

```bash
curl -s -X POST "http://localhost:5099/api/approvals/00000000-0000-0000-0000-000000000000/approve" \
  -H "Content-Type: application/json" \
  -d '{"approverId":"user1","comment":"test"}'
```

**响应：** HTTP 404

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态 | 404 | 404 | ✅ Pass |

### 4.4 对非 WaitingApproval 会话 approve

**命令：**

```bash
curl -s -X POST "http://localhost:5099/api/approvals/1ce8d083-b38b-4fe1-9e2c-2b80f2e23b78/approve" \
  -H "Content-Type: application/json" \
  -d '{"approverId":"user1","comment":"test"}'
```

（会话 1ce8d083 已 reject 为 Cancelled）

**响应：** HTTP 400

```json
{"error":"Session status must be WaitingApproval, current: Cancelled"}
```

| 校验项 | 预期 | 实际 | Pass/Fail |
|--------|------|------|-----------|
| HTTP 状态 | 400 | 400 | ✅ Pass |
| 错误信息 | 明确说明状态不符 | 是 | ✅ Pass |

### 4.5 limit 非法

| 接口 | 参数 | 预期 | 实际 | Pass/Fail |
|------|------|------|------|-----------|
| pending | limit=0 | 400 | 400, `limit must be between 1 and 100` | ✅ Pass |
| pending | limit=101 | 400 | 400 | ✅ Pass |
| history | limit=0 | 400 | 400, `limit must be between 1 and 200` | ✅ Pass |
| history | limit=201 | 400 | 400 | ✅ Pass |

---

## 5. 结论

- **US-009 Approvals 待审批与审批历史**：**Pass**
- **GET /api/approvals/pending**：返回 WaitingApproval 会话，结构正确
- **POST /api/approvals/{sessionId}/approve**：状态变为 Running，记录进入 history
- **POST /api/approvals/{sessionId}/reject**：状态变为 Cancelled，记录进入 history
- **GET /api/approvals/history**：返回 approve/reject 记录，含 approver、时间、动作
- **错误分支**：approverId 空/空白返回 400；404/400 符合契约

---

## 6. 风险与后续

| 项目 | 说明 |
|------|------|
| **数据准备** | 本次使用 DB 直接更新；若需从零准备，可先 POST analyze 再 UPDATE |
| **前端 E2E** | 已追加第 7 章完成前端 Approvals 页面联调验证 |
| **后续范围** | `backend-api-p1` 仍有 approval rules 等能力待实现 |

---

## 7. 前端 Approvals 页面 E2E（2026-03-12）

### 7.1 验证目标

1) 打开 `/approvals` 页面  
2) 验证 Pending Approvals 与 Approval History 区块可见  
3) 若有至少 1 条待审批项，在页面执行一次 Approve 或 Reject  
4) 验证操作后页面状态更新（待审批数量变化或历史新增记录）  
5) 记录 Pass/Fail 与关键观察  

### 7.2 验证步骤与结果

| 步骤 | 预期 | 实际 | Pass/Fail |
|------|------|------|-----------|
| 1. 打开 /approvals | 页面可加载 | 页面加载成功 | Pass |
| 2. Pending Approvals 区块可见 | 区块存在 | heading「Pending Approvals ( 1 )」可见 | Pass |
| 3. Approval History 区块可见 | 区块存在 | heading「Approval History ( 2 )」可见 | Pass |
| 4. 至少 1 条待审批项 + 执行操作 | 若有则可见并执行 Approve/Reject | 1 条待审批项（inventory-service-log-errors-dev），点击 Approve | Pass |
| 5. 操作后状态更新 | 待审批数量变化或历史新增 | Pending: 1→0（显示「当前无待审批项。」）；History: 2→3（新增 Approve · oncall-user） | Pass |

### 7.3 关键观察

- **Pending Approvals**：初始 1 条，含 alertName、serviceName、status、updatedAt，每项有 Approve/Reject 按钮
- **Approval History**：初始 2 条，含 action、intervenedBy、reason、intervenedAt
- **操作后**：待审批项从列表移除，历史新增 1 条 Approve 记录（Approve · oncall-user · 无备注 · 3/12/2026, 9:16:55 PM）
- **审批参数**：Approver ID 默认 oncall-user，Comment 可选

### 7.4 结论

| 项目 | 结果 |
|------|------|
| **前端 Approvals E2E** | **Pass** |
| **Pending Approvals** | 区块可见，1 条待审批，执行 Approve 成功 |
| **Approval History** | 区块可见，操作后新增 1 条记录 |
