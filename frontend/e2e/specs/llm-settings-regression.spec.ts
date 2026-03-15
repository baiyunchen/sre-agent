/**
 * User-centric regression suite for Settings -> LLM Configuration.
 * Run: npx playwright test e2e/specs/llm-settings-regression.spec.ts --project=chromium
 */
import { expect, test, type APIRequestContext, type Page } from "@playwright/test"

const APP_BASE_URL = process.env.E2E_APP_BASE_URL ?? "http://localhost:5173"
const API_BASE_URL = "http://localhost:5099"

type LlmConfigResponse = {
  provider: string
  models: Record<string, string>
}

async function getLlmConfig(request: APIRequestContext): Promise<LlmConfigResponse> {
  const response = await request.get(`${API_BASE_URL}/api/settings/llm`)
  expect(response.ok(), `GET /api/settings/llm failed with ${response.status()}`).toBeTruthy()
  return (await response.json()) as LlmConfigResponse
}

async function updateLlmConfig(
  request: APIRequestContext,
  payload: { provider: "AliyunBailian" | "Zhipu"; apiKey?: string; models?: Record<string, string> },
): Promise<void> {
  const response = await request.put(`${API_BASE_URL}/api/settings/llm`, {
    data: payload,
  })
  expect(response.ok(), `PUT /api/settings/llm failed with ${response.status()}`).toBeTruthy()
}

async function openLlmTab(page: Page): Promise<void> {
  await page.goto(`${APP_BASE_URL}/settings`)
  await page.getByRole("tab", { name: "LLM Configuration" }).click()
  await expect(page.locator("#model-provider")).toBeVisible()
}

async function selectProvider(page: Page, providerLabel: "Aliyun Bailian" | "Zhipu AI"): Promise<void> {
  await page.locator("#model-provider").click()
  await page.getByText(providerLabel).click()
}

function saveButton(page: Page) {
  return page.getByRole("button", { name: /Save LLM Settings|Save Changes/i })
}

test.describe("LLM Settings Regression", () => {
  test.describe.configure({ mode: "serial" })

  let originalConfig: LlmConfigResponse

  test.beforeAll(async ({ request }) => {
    originalConfig = await getLlmConfig(request)
    await updateLlmConfig(request, {
      provider: "AliyunBailian",
      apiKey: "sk-e2e-aliyun-seed",
    })
  })

  test.afterAll(async ({ request }) => {
    await updateLlmConfig(request, {
      provider:
        originalConfig.provider === "Zhipu"
          ? "Zhipu"
          : "AliyunBailian",
      apiKey:
        originalConfig.provider === "Zhipu"
          ? "sk-e2e-restore-zhipu"
          : "sk-e2e-restore-aliyun",
      models: originalConfig.models,
    })
  })

  test("1) 页面加载后展示后端配置与能力映射", async ({ page }) => {
    await openLlmTab(page)

    await expect(page.getByLabel("Model Provider")).toBeVisible()
    await expect(page.getByText("Model Capability Mapping")).toBeVisible()
    await expect(page.getByText("Large")).toBeVisible()
    await expect(page.getByText("Medium")).toBeVisible()
    await expect(page.getByText("Small")).toBeVisible()
    await expect(page.getByText("Reasoning")).toBeVisible()
    await expect(page.getByText("Coding")).toBeVisible()
  })

  test("2) 切换到 Zhipu 不填 key 保存应失败，且刷新后不应生效", async ({ page, request }) => {
    await updateLlmConfig(request, {
      provider: "AliyunBailian",
      apiKey: "sk-e2e-aliyun-seed",
    })

    await openLlmTab(page)
    await expect(page.locator("#model-provider")).toContainText("Aliyun")

    await selectProvider(page, "Zhipu AI")
    await page.locator("#api-key").fill("")
    await saveButton(page).click()

    await expect(
      page.getByText(/requires a new API key|required for new provider|Failed to update/i).first(),
    ).toBeVisible()

    await page.reload()
    await page.getByRole("tab", { name: "LLM Configuration" }).click()
    await expect(page.locator("#model-provider")).toContainText("Aliyun")
  })

  test("3) 切换 Provider 成功后模型映射应联动更新，并可切回 Aliyun", async ({ page, request }) => {
    await updateLlmConfig(request, {
      provider: "AliyunBailian",
      apiKey: "sk-e2e-aliyun-seed",
    })

    await openLlmTab(page)
    await selectProvider(page, "Zhipu AI")
    await page.locator("#api-key").fill("sk-e2e-zhipu-seed")
    await saveButton(page).click()

    await expect(page.getByText(/updated successfully|success/i).first()).toBeVisible()
    await expect.poll(async () => (await getLlmConfig(request)).provider).toBe("Zhipu")
    await expect(page.getByText("glm-4.6").first()).toBeVisible()

    await selectProvider(page, "Aliyun Bailian")
    await page.locator("#api-key").fill("sk-e2e-aliyun-seed")
    await saveButton(page).click()

    await expect(page.getByText(/updated successfully|success/i).first()).toBeVisible()
    await expect.poll(async () => (await getLlmConfig(request)).provider).toBe("AliyunBailian")
    await expect(page.getByText("qwen3.5-plus").first()).toBeVisible()
  })
})
