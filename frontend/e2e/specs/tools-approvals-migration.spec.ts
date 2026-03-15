/**
 * User-centric E2E: Tools approval management should be reliable and actionable.
 * Run: npx playwright test e2e/specs/tools-approvals-migration.spec.ts --project=chromium
 */
import { expect, test, type APIRequestContext, type Locator, type Page } from "@playwright/test"

const APP_BASE_URL = process.env.E2E_APP_BASE_URL ?? "http://localhost:5173"
const API_BASE_URL = "http://localhost:5099"
const TEST_OPERATOR = "playwright-e2e"

type ToolRegistryItem = {
  name: string
  approvalMode: "auto-approve" | "require-approval" | "always-deny"
  autoApprove: boolean
}

type ToolRegistryResponse = {
  items: ToolRegistryItem[]
  total: number
}

type RuleRecord = {
  id: string
  toolName: string
  ruleType: "always-allow" | "always-deny" | "require-approval"
}

async function fetchToolRegistry(request: APIRequestContext): Promise<ToolRegistryResponse> {
  const response = await request.get(`${API_BASE_URL}/api/tools`)
  expect(response.ok(), `GET /api/tools failed with ${response.status()}`).toBeTruthy()
  return (await response.json()) as ToolRegistryResponse
}

async function createRule(
  request: APIRequestContext,
  toolName: string,
  ruleType: "always-allow" | "always-deny" | "require-approval",
): Promise<RuleRecord> {
  const response = await request.post(`${API_BASE_URL}/api/approvals/rules`, {
    data: {
      toolName,
      ruleType,
      createdBy: TEST_OPERATOR,
    },
  })
  expect(response.ok(), `POST /api/approvals/rules failed with ${response.status()}`).toBeTruthy()
  return (await response.json()) as RuleRecord
}

async function deleteRule(request: APIRequestContext, ruleId: string): Promise<void> {
  const response = await request.delete(`${API_BASE_URL}/api/approvals/rules/${ruleId}`)
  expect(
    response.ok(),
    `DELETE /api/approvals/rules/${ruleId} failed with ${response.status()}`,
  ).toBeTruthy()
}

function toolCard(page: Page, toolName: string): Locator {
  return page
    .getByText(toolName, { exact: true })
    .first()
    .locator("xpath=ancestor::div[.//*[@role='switch']][1]")
}

function toolSwitch(page: Page, toolName: string): Locator {
  return toolCard(page, toolName).locator('[role="switch"]')
}

function toolStatusText(page: Page, toolName: string): Locator {
  return toolCard(page, toolName).getByText(/Auto Approve|Require Approval|Always Deny/)
}

test.describe("Tools 审批管理 - 用户视角 E2E", () => {
  test("1) Tools 页面展示真实工具与关键指标", async ({ page, request }) => {
    const registry = await fetchToolRegistry(request)
    expect(registry.total).toBeGreaterThan(0)

    await page.goto(`${APP_BASE_URL}/tools`)
    await expect(page.getByRole("heading", { name: "Tools & Agents Registry" })).toBeVisible()
    await expect(page.getByText("Loading tools from backend...")).toHaveCount(0)
    await expect(page.getByText("获取工具注册表失败")).toHaveCount(0)

    const firstTool = registry.items[0]
    await expect(page.getByText(firstTool.name, { exact: true })).toBeVisible()
    await expect(toolCard(page, firstTool.name).getByText("Invocations")).toBeVisible()
    await expect(toolCard(page, firstTool.name).getByText("Success")).toBeVisible()
    await expect(toolCard(page, firstTool.name).getByText("Avg Time")).toBeVisible()
  })

  test("2) Auto Approve 开关可切换并在刷新后保持", async ({ page, request }) => {
    const registry = await fetchToolRegistry(request)
    const candidate = registry.items.find((item) => item.approvalMode !== "always-deny")
    expect(candidate, "No toggleable tool found for auto-approve test").toBeDefined()

    const tool = candidate!
    const originalAutoApprove = tool.autoApprove
    const targetAutoApprove = !originalAutoApprove

    await page.goto(`${APP_BASE_URL}/tools`)
    await expect(toolSwitch(page, tool.name)).toBeVisible()
    await expect(toolSwitch(page, tool.name)).toHaveAttribute(
      "aria-checked",
      String(originalAutoApprove),
    )

    await toolSwitch(page, tool.name).click()

    await expect.poll(async () => {
      const latest = await fetchToolRegistry(request)
      return latest.items.find((item) => item.name === tool.name)?.autoApprove
    }).toBe(targetAutoApprove)

    await page.reload()
    await expect(toolSwitch(page, tool.name)).toHaveAttribute(
      "aria-checked",
      String(targetAutoApprove),
    )

    await toolSwitch(page, tool.name).click()
    await expect.poll(async () => {
      const latest = await fetchToolRegistry(request)
      return latest.items.find((item) => item.name === tool.name)?.autoApprove
    }).toBe(originalAutoApprove)
  })

  test("3) 已存在 always-deny 规则时，Tools 页面应可识别并覆盖", async ({ page, request }) => {
    const registry = await fetchToolRegistry(request)
    const targetTool = registry.items[0]
    let createdRuleId: string | null = null

    try {
      const rule = await createRule(request, targetTool.name, "always-deny")
      createdRuleId = rule.id

      await expect.poll(async () => {
        const latest = await fetchToolRegistry(request)
        return latest.items.find((item) => item.name === targetTool.name)?.approvalMode
      }).toBe("always-deny")

      await page.goto(`${APP_BASE_URL}/tools`)
      await expect(toolStatusText(page, targetTool.name)).toContainText("Always Deny")
      await expect(toolSwitch(page, targetTool.name)).toHaveAttribute("aria-checked", "false")

      await toolSwitch(page, targetTool.name).click()
      await expect.poll(async () => {
        const latest = await fetchToolRegistry(request)
        return latest.items.find((item) => item.name === targetTool.name)?.approvalMode
      }).toBe("auto-approve")
      await expect(toolStatusText(page, targetTool.name)).toContainText("Auto Approve")
    } finally {
      if (createdRuleId) {
        await deleteRule(request, createdRuleId)
      }
    }
  })

  test("4) Approvals 页面仅保留 Pending/History，不再展示 Rules", async ({ page }) => {
    await page.goto(`${APP_BASE_URL}/approvals`)
    await expect(page.getByRole("heading", { name: "Approval Management" })).toBeVisible()
    await expect(page.getByRole("tab", { name: /Pending/i })).toBeVisible()
    await expect(page.getByRole("tab", { name: /History/i })).toBeVisible()
    await expect(page.getByRole("tab", { name: /Rules/i })).toHaveCount(0)
  })

  test("5) Tools 接口异常时应有可读错误反馈", async ({ page }) => {
    await page.route("**/api/tools*", (route) => route.abort("failed"))

    await page.goto(`${APP_BASE_URL}/tools`)
    await expect(page.getByText("Loading tools from backend...")).toHaveCount(0, { timeout: 10000 })
    await expect(page.getByText(/获取工具注册表失败|Failed to fetch/i)).toBeVisible()
  })
})
