/**
 * E2E test for LLM Configuration settings page
 * Run: npx playwright test e2e-llm-settings.spec.ts --project=chromium
 */
import { test, expect } from "@playwright/test"

const BASE_URL = "http://localhost:5174"
const SCREENSHOT_DIR = "e2e-screenshots"

test.describe("LLM Configuration Settings E2E", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(`${BASE_URL}/settings`)
  })

  test("1. Settings page loads correctly", async ({ page }) => {
    await expect(page.locator("h1")).toContainText("Settings")
    await page.screenshot({ path: `${SCREENSHOT_DIR}/01-settings-page-loaded.png` })
  })

  test("2. LLM Configuration tab shows backend data", async ({ page }) => {
    await page.getByRole("tab", { name: "LLM Configuration" }).click()
    await page.waitForTimeout(1500)
    await expect(page.getByLabel("Model Provider")).toBeVisible()
    await expect(page.getByText("Model Capability Mapping")).toBeVisible()
    await page.screenshot({ path: `${SCREENSHOT_DIR}/02-llm-config-loaded.png` })
  })

  test("3. Model Capability Mapping table has expected entries", async ({ page }) => {
    await page.getByRole("tab", { name: "LLM Configuration" }).click()
    await page.waitForTimeout(1500)
    const capabilities = ["Large", "Medium", "Small", "Reasoning", "Coding"]
    for (const cap of capabilities) {
      await expect(page.getByText(cap, { exact: false })).toBeVisible()
    }
  })

  test("4. Provider dropdown shows Aliyun and Zhipu options", async ({ page }) => {
    await page.getByRole("tab", { name: "LLM Configuration" }).click()
    await page.waitForTimeout(1500)
    await page.locator("#model-provider").click()
    await expect(page.getByText("Aliyun Bailian")).toBeVisible()
    await expect(page.getByText("Zhipu AI")).toBeVisible()
    await page.screenshot({ path: `${SCREENSHOT_DIR}/04-provider-dropdown.png` })
  })

  test("5. Switch to Zhipu and save", async ({ page }) => {
    await page.getByRole("tab", { name: "LLM Configuration" }).click()
    await page.waitForTimeout(1500)
    await page.locator("#model-provider").click()
    await page.getByText("Zhipu AI").click()
    await page.getByRole("button", { name: "Save LLM Settings" }).click()
    await expect(page.getByText("LLM configuration updated successfully")).toBeVisible({ timeout: 5000 })
    await page.waitForTimeout(1000)
    await page.screenshot({ path: `${SCREENSHOT_DIR}/05-zhipu-saved.png` })
  })

  test("6. Model mapping shows Zhipu models after switch", async ({ page }) => {
    await page.getByRole("tab", { name: "LLM Configuration" }).click()
    await page.waitForTimeout(1500)
    await page.locator("#model-provider").click()
    await page.getByText("Zhipu AI").click()
    await page.getByRole("button", { name: "Save LLM Settings" }).click()
    await expect(page.getByText("LLM configuration updated successfully")).toBeVisible({ timeout: 5000 })
    await page.waitForTimeout(2000)
    await expect(page.getByText("glm-4.6").first()).toBeVisible({ timeout: 3000 })
    await expect(page.getByText("codegeex").first()).toBeVisible({ timeout: 3000 })
  })

  test("7. Switch back to Aliyun and restore default", async ({ page }) => {
    await page.getByRole("tab", { name: "LLM Configuration" }).click()
    await page.waitForTimeout(1500)
    await page.locator("#model-provider").click()
    await page.getByText("Aliyun Bailian").click()
    await page.getByRole("button", { name: "Save LLM Settings" }).click()
    await expect(page.getByText("LLM configuration updated successfully")).toBeVisible({ timeout: 5000 })
    await page.waitForTimeout(1000)
    await page.screenshot({ path: `${SCREENSHOT_DIR}/07-aliyun-restored.png` })
  })
})
