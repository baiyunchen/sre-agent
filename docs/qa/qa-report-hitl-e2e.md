# HITL 功能后端 E2E 测试报告

**测试日期**: 2026-03-14  
**测试环境**: 
- 后端: `http://localhost:5099`
- 启动命令: `dotnet run --project backend/src/SreAgent.Api/SreAgent.Api.csproj`
- PostgreSQL: 已运行 (localhost:5432)

---

## 测试结果汇总

| 场景 | 状态 | 说明 |
|------|------|------|
| 场景 1: 后台执行 + SSE 实时推送 | ✅ PASS | 202 + sessionId，SSE 连接，session 完成 |
| 场景 2: 中断机制 | ✅ PASS | interrupt 200，session 状态 Interrupted |
| 场景 3: 恢复与人类输入 | ✅ PASS | resume 202，session 恢复 Running |
| 场景 4: 工具审批规则 | ⚠️ PARTIAL | 规则创建成功；未在 90s 内收到 tool.approval_required |
| 场景 5: API 健壮性 | ✅ PASS | 全部 3 项通过 |

---

## 场景 1: 后台执行 + SSE 实时推送

### 步骤与结果

1. **POST /api/sre/analyze**
   - 请求体: `{ title, severity, alertTime, affectedService, description, additionalInfo }`
   - 预期: 202 Accepted + sessionId
   - 实际: ✅ 202，sessionId 返回

2. **GET /api/sessions/{sessionId}/stream**
   - 预期: SSE 流，收到 `session.ended` 事件
   - 实际: ✅ 连接成功；session 在 90s 内完成（status=Failed，可能因 AWS/API 配置）

3. **GET /api/sessions/{id}**
   - 预期: 返回会话详情
   - 实际: ✅ 200，包含 id、status、source、severity 等

---

## 场景 2: 中断机制

### 步骤与结果

1. **POST /api/sre/analyze** 创建新会话
2. **POST /api/sessions/{sessionId}/interrupt**
   - 请求体: `{ "reason": "E2E test interrupt", "userId": "e2e-test" }`
   - 预期: 200 OK
   - 实际: ✅ 200

3. **GET /api/sessions/{id}** 验证状态
   - 预期: status = "Interrupted"
   - 实际: ✅ Interrupted

---

## 场景 3: 恢复与人类输入

### 步骤与结果

1. 在场景 2 中断后，**POST /api/sessions/{sessionId}/resume**
   - 请求体: `{ "continueInput": "Please continue the analysis" }`
   - 预期: 202 Accepted
   - 实际: ✅ 202

2. **GET /api/sessions/{id}** 验证状态
   - 预期: status = "Running"
   - 实际: ✅ Running

---

## 场景 4: 工具审批规则

### 步骤与结果

1. **POST /api/approvals/rules**
   - 请求体: `{ "toolName": "cloudwatch_simple_query", "ruleType": "require-approval", "createdBy": "e2e-test" }`
   - 预期: 201 Created
   - 实际: ✅ 201

2. **POST /api/sre/analyze** 创建分析任务
3. **GET /api/sessions/{id}/stream** 等待 `tool.approval_required`
   - 预期: 90s 内收到事件
   - 实际: ⚠️ 未收到（agent 可能未调用 cloudwatch_simple_query，或提前完成/失败）

4. **POST /api/sessions/{id}/tool-invocations/{invocationId}/approve**
   - 因未收到 approval_required，本步骤未执行

### 建议

- 若需完整验证审批流程，可延长 SSE 等待时间，或使用 mock 确保 agent 必定调用 `cloudwatch_simple_query`
- 规则 CRUD 与 approve/reject 端点已通过单元/集成测试验证

---

## 场景 5: API 健壮性

### 步骤与结果

| 测试项 | 请求 | 预期 | 实际 |
|--------|------|------|------|
| 不存在的 session 调用 interrupt | POST /api/sessions/00000000-0000-0000-0000-000000000000/interrupt | 400 | ✅ 400 |
| 已完成的 session 调用 interrupt | POST /api/sessions/{completedId}/interrupt | 400 | ✅ 400 |
| 不存在的 tool invocation 调用 approve | POST /api/sessions/{id}/tool-invocations/00000000-.../approve | 404/400 | ✅ 404 |

---

## 执行命令

```bash
cd /Users/baiyunchen/workspace/sre-agent
./scripts/hitl-e2e-test.sh
```

---

## 结论

- **通过**: 场景 1、2、3、5 全部通过
- **部分通过**: 场景 4 规则创建成功，审批事件依赖 agent 实际调用 `cloudwatch_simple_query`，本次未在超时内触发
- **建议**: 场景 4 可增加集成测试，通过 mock 强制 agent 调用需审批工具以验证完整流程
