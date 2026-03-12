import type {
  SessionListResponse,
  SessionMessageRequest,
  SessionMessageResponse,
  SessionsQuery,
} from "@/app/lib/types"

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5099"

function buildQueryString(query: SessionsQuery): string {
  const params = new URLSearchParams()
  Object.entries(query).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") {
      return
    }
    params.set(key, String(value))
  })
  const output = params.toString()
  return output ? `?${output}` : ""
}

export async function fetchSessions(
  query: SessionsQuery = {},
): Promise<SessionListResponse> {
  const queryString = buildQueryString(query)
  const response = await fetch(`${API_BASE_URL}/api/sessions${queryString}`)

  if (!response.ok) {
    const fallbackMessage = `获取 Sessions 失败: ${response.status}`
    const errorPayload = (await response.json().catch(() => null)) as
      | { error?: string }
      | null
    throw new Error(errorPayload?.error ?? fallbackMessage)
  }

  return (await response.json()) as SessionListResponse
}

export async function postSessionMessage(
  sessionId: string,
  payload: SessionMessageRequest,
): Promise<SessionMessageResponse> {
  const response = await fetch(`${API_BASE_URL}/api/sessions/${sessionId}/messages`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  })

  if (!response.ok) {
    const fallbackMessage = `发送消息失败: ${response.status}`
    const errorPayload = (await response.json().catch(() => null)) as
      | { error?: string }
      | null
    throw new Error(errorPayload?.error ?? fallbackMessage)
  }

  return (await response.json()) as SessionMessageResponse
}
