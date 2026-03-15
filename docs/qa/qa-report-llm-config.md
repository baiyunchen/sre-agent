# QA Report: LLM Configuration Settings Page

**Feature:** LLM Configuration (llm-config)  
**Test Date:** 2026-03-14  
**Test Type:** Browser-based E2E (Playwright)  
**Environment:** Frontend `localhost:5174`, Backend `localhost:5099`

---

## Summary

| Metric | Result |
|--------|--------|
| **Verdict** | **PASS** |
| Tests Executed | 7 |
| Tests Passed | 7 |
| Tests Failed | 0 |

---

## Preconditions

- Frontend dev server running on `http://localhost:5174`
- Backend API running on `http://localhost:5099`
- CORS configured for `localhost:5174`
- LLM settings API endpoints: `GET/PUT /api/settings/llm`, `GET /api/settings/llm/providers`

---

## Test Steps and Results

### 1. Settings page loads correctly
- **Action:** Navigate to `http://localhost:5174/settings`
- **Expected:** Page loads with Settings heading and tab navigation
- **Actual:** PASS — Settings page loads, General tab active by default
- **Evidence:** `frontend/e2e-screenshots/01-settings-page-loaded.png`

### 2. LLM Configuration tab shows backend data
- **Action:** Click "LLM Configuration" tab
- **Expected:** Model Provider, Base URL, API Key status, Model Capability Mapping table visible
- **Actual:** PASS — All elements visible; provider "AliyunBailian", Base URL `https://dashscope.aliyuncs.com/compatible-mode/v1`, API Key "Configured"
- **Evidence:** `frontend/e2e-screenshots/02-llm-config-loaded.png`

### 3. Model Capability Mapping table has expected entries
- **Action:** Verify table rows
- **Expected:** Large, Medium, Small, Reasoning, Coding
- **Actual:** PASS — All five capabilities present with Aliyun models (qwen3.5-plus, qwen-turbo)

### 4. Provider dropdown shows correct options
- **Action:** Open Model Provider dropdown
- **Expected:** "Aliyun Bailian (通义千问)" and "Zhipu AI (智谱清言)"
- **Actual:** PASS — Both options visible in dropdown
- **Evidence:** `frontend/e2e-screenshots/04-provider-dropdown.png`

### 5. Switch to Zhipu and save
- **Action:** Select "Zhipu AI (智谱清言)", click "Save LLM Settings"
- **Expected:** Success toast "LLM configuration updated successfully!"
- **Actual:** PASS — Toast displayed, provider switched to Zhipu
- **Evidence:** `frontend/e2e-screenshots/05-zhipu-saved.png`

### 6. Model mapping shows Zhipu models after switch
- **Action:** Verify model table after Zhipu selection
- **Expected:** glm-4.6, glm-4.7-flash, codegeex-4, etc.
- **Actual:** PASS — Table shows glm-4.6, glm-4.7-flash, glm-4.7, codegeex-4

### 7. Switch back to Aliyun and restore default
- **Action:** Select "Aliyun Bailian (通义千问)", click "Save LLM Settings"
- **Expected:** Success toast, model table reverts to Aliyun models
- **Actual:** PASS — Toast "Provider switched to AliyunBailian", table shows qwen3.5-plus, qwen-turbo
- **Evidence:** `frontend/e2e-screenshots/07-aliyun-restored.png`

---

## Findings

### Did the page load correctly with real backend data?
**Yes.** The LLM Configuration tab loads configuration from the backend:
- Provider name (AliyunBailian)
- Base URL (dashscope.aliyuncs.com)
- API Key status (Configured with masked hint)
- Model capability mapping (qwen3.5-plus, qwen-turbo)

### Did the provider dropdown show correct options?
**Yes.** Both options are present:
- Aliyun Bailian (通义千问)
- Zhipu AI (智谱清言)

### Did switching providers work?
**Yes.** Switching to Zhipu and back to Aliyun both succeed. Success toasts appear and the UI updates accordingly.

### Did the model mapping table update correctly?
**Yes.** The table updates per provider:
- **Aliyun:** qwen3.5-plus, qwen-turbo
- **Zhipu:** glm-4.6, glm-4.7-flash, glm-4.7, codegeex-4

### Were there any errors or UX issues?
**No.** No console errors, CORS issues, or UX problems observed. The flow is consistent and responsive.

---

## Screenshots

| Step | File | Description |
|------|------|-------------|
| 1 | `01-settings-page-loaded.png` | Settings page initial load |
| 2 | `02-llm-config-loaded.png` | LLM config with Aliyun data |
| 4 | `04-provider-dropdown.png` | Provider dropdown with both options |
| 5 | `05-zhipu-saved.png` | Zhipu config with success toast |
| 7 | `07-aliyun-restored.png` | Aliyun restored with success toast |

Screenshots location: `frontend/e2e-screenshots/`

---

## Release Recommendation

**GO** — LLM Configuration settings page is ready for release. All E2E scenarios pass.

---

## Regression Addendum (US-L05)

**Date:** 2026-03-14  
**Scope:** 保存逻辑与用户路径回归（provider 切换 + API key 校验）

### Additional Scenarios

1. 切换到 `Zhipu` 且不输入 API key，点击保存
   - **Expected:** 保存失败，展示错误提示，配置不应落库/生效
   - **Actual:** PASS — 错误提示 `Switching provider requires a new API key`，刷新后 provider 仍为 `AliyunBailian`
2. 切换到 `Zhipu` 并输入测试 key，点击保存
   - **Expected:** 保存成功，刷新后 provider 为 `Zhipu`
   - **Actual:** PASS — 成功提示，刷新后 provider 为 `Zhipu`，模型映射为 Zhipu 默认模型

### Evidence

- `frontend/e2e-screenshots/save-01-initial-state.png`
- `frontend/e2e-screenshots/save-02-after-save-no-apikey.png`
- `frontend/e2e-screenshots/save-03-after-refresh.png`
- `frontend/e2e-screenshots/save-04-after-save-with-apikey.png`
- `frontend/e2e-screenshots/save-05-final-zhipu-saved.png`

### Updated Recommendation

**GO** — US-L05 回归通过，关键失败分支和成功分支均符合用户预期。

---

## Regression Addendum (US-L06)

**Date:** 2026-03-14  
**Scope:** 持久化与分层重构回归（`llm_settings` + Service/Repository）

### Additional Scenarios

1. 持久化恢复验证（API）
   - **Action:** 保存 `Zhipu` 配置后重启后端，再次 `GET /api/settings/llm`
   - **Expected:** 重启后仍返回 `Zhipu` 配置
   - **Actual:** PASS — 启动日志出现 `Loaded persisted llm settings from database for provider: Zhipu`，接口返回保持一致
2. 前端关键链路验证（Browser）
   - **Action:** Zhipu 无 key 保存 -> 失败；补 key 保存 -> 成功；切回 Aliyun -> 成功
   - **Expected:** 失败/成功分支均符合预期，模型映射与 provider 一致
   - **Actual:** PASS — 全部符合

### Evidence

- API 持久化验证：
  - 重启前保存响应：`provider=Zhipu`
  - 重启后读取响应：`provider=Zhipu`
  - 启动日志：`Loaded persisted llm settings from database for provider: Zhipu`
- Browser 截图：
  - `frontend/e2e-screenshots/flow-01-zhipu-no-key-fail.png`
  - `frontend/e2e-screenshots/flow-02-zhipu-with-key-success.png`
  - `frontend/e2e-screenshots/flow-03-zhipu-models-mapping.png`
  - `frontend/e2e-screenshots/flow-04-aliyun-with-key-success.png`

### Updated Recommendation

**GO** — US-L06 回归通过，设置持久化能力与分层治理符合验收标准。

---

## Regression Addendum (US-L07)

**Date:** 2026-03-14  
**Scope:** Tools 页面后端集成 + Approval Rules 入口迁移

### Additional Scenarios

1. Tools 页面真实数据加载
   - **Action:** 打开 `http://localhost:5174/tools`
   - **Expected:** 能看到后端工具列表（非静态 mock），包含工具名、统计和审批状态
   - **Actual:** PASS — 页面加载后显示后端返回工具（如 `cloudwatch_simple_query`、`todo_write`）
2. Auto Approve 开关联动
   - **Action:** 在 Tools 页面切换某工具 Auto Approve 开关
   - **Expected:** 状态即时变化，且无前端错误
   - **Actual:** PASS — 开关可切换，页面状态同步更新
3. Approvals 页面入口收敛
   - **Action:** 打开 `http://localhost:5174/approvals`
   - **Expected:** 仅有 `Pending`、`History` 两个 tab，不再展示 `Rules`
   - **Actual:** PASS — 仅保留 `Pending` 与 `History`

### Notes

- 首轮 E2E 失败原因为后端旧进程仍在运行（`/api/tools` 返回 404）；重启后端加载新代码后复测通过。

### Updated Recommendation

**GO** — US-L07 回归通过，Tools 与 Approvals 的规则管理入口已按预期收敛。

---

## Regression Addendum (US-L08)

**Date:** 2026-03-14  
**Scope:** Tools 审批能力用户旅程 E2E 用例补强

### Test Design Upgrades

- 将原“页面可见性”验证升级为“用户旅程级”回归：
  1. Tools 真实工具数据 + 指标展示
  2. Auto Approve 开关切换、刷新持久化与回滚恢复
  3. `always-deny` 规则识别与覆盖行为验证
  4. Approvals 页面 Rules 入口移除验证
  5. `/api/tools` 异常时可读错误反馈

### Execution Result

- Command:
  - `npx playwright test e2e-tools-approvals-migration.spec.ts --project=chromium`
- Result:
  - **PASS (5/5)**
  - 用时约 12.5s

### Updated Recommendation

**GO** — 审批相关关键路径具备稳定 E2E 回归保障，可用于后续迭代防回归。
