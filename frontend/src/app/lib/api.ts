import type {
  DashboardActiveSessionsResponse,
  DashboardStatsResponse,
  SessionListResponse,
  SessionDiagnosisResponse,
  SessionMessageRequest,
  SessionMessageResponse,
  SessionTimelineResponse,
  SessionTodoItem,
  SessionTodosResponse,
  SessionToolInvocationsResponse,
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

export async function fetchSessionTimeline(
  sessionId: string,
): Promise<SessionTimelineResponse> {
  const response = await fetch(`${API_BASE_URL}/api/sessions/${sessionId}/timeline`)
  if (!response.ok) {
    const fallbackMessage = `获取时间线失败: ${response.status}`
    const errorPayload = (await response.json().catch(() => null)) as
      | { error?: string }
      | null
    throw new Error(errorPayload?.error ?? fallbackMessage)
  }

  return (await response.json()) as SessionTimelineResponse
}

export async function fetchSessionDiagnosis(
  sessionId: string,
): Promise<SessionDiagnosisResponse> {
  const response = await fetch(`${API_BASE_URL}/api/sessions/${sessionId}/diagnosis`)
  if (!response.ok) {
    const fallbackMessage = `获取诊断失败: ${response.status}`
    const errorPayload = (await response.json().catch(() => null)) as
      | { error?: string }
      | null
    throw new Error(errorPayload?.error ?? fallbackMessage)
  }

  return (await response.json()) as SessionDiagnosisResponse
}

export async function fetchSessionToolInvocations(
  sessionId: string,
): Promise<SessionToolInvocationsResponse> {
  const response = await fetch(`${API_BASE_URL}/api/sessions/${sessionId}/tool-invocations`)
  if (!response.ok) {
    const fallbackMessage = `获取工具调用失败: ${response.status}`
    const errorPayload = (await response.json().catch(() => null)) as
      | { error?: string }
      | null
    throw new Error(errorPayload?.error ?? fallbackMessage)
  }

  return (await response.json()) as SessionToolInvocationsResponse
}

export async function fetchSessionTodos(
  sessionId: string,
): Promise<SessionTodosResponse> {
  const response = await fetch(`${API_BASE_URL}/api/sessions/${sessionId}/todos`)
  if (!response.ok) {
    const fallbackMessage = `获取 Todo 失败: ${response.status}`
    const errorPayload = (await response.json().catch(() => null)) as
      | { error?: string }
      | null
    throw new Error(errorPayload?.error ?? fallbackMessage)
  }

  const payload = (await response.json()) as SessionTodosResponse
  return {
    ...payload,
    items: payload.items ?? ([] as SessionTodoItem[]),
  }
}

export async function fetchDashboardStats(): Promise<DashboardStatsResponse> {
  const response = await fetch(`${API_BASE_URL}/api/dashboard/stats`)
  if (!response.ok) {
    const fallbackMessage = `获取 Dashboard 统计失败: ${response.status}`
    const errorPayload = (await response.json().catch(() => null)) as
      | { error?: string }
      | null
    throw new Error(errorPayload?.error ?? fallbackMessage)
  }

  return (await response.json()) as DashboardStatsResponse
}

export async function fetchDashboardActiveSessions(
  limit = 10,
): Promise<DashboardActiveSessionsResponse> {
  const response = await fetch(`${API_BASE_URL}/api/dashboard/active-sessions?limit=${limit}`)
  if (!response.ok) {
    const fallbackMessage = `获取活跃会话失败: ${response.status}`
    const errorPayload = (await response.json().catch(() => null)) as
      | { error?: string }
      | null
    throw new Error(errorPayload?.error ?? fallbackMessage)
  }

  return (await response.json()) as DashboardActiveSessionsResponse
}
