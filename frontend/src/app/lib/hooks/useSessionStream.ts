import { useCallback, useEffect, useMemo, useState } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { getApiBaseUrl } from "@/app/lib/api"
import type { ToolApprovalRequiredPayload } from "@/app/lib/types"

export type SessionStreamStatus = "connecting" | "connected" | "disconnected"

export interface PendingToolApproval {
  invocationId: string
  toolName: string
  parameters?: string
}

export type AgentActivity =
  | { phase: "thinking" }
  | { phase: "tool"; toolName: string }
  | { phase: "approval"; toolName: string }
  | null

/**
 * Subscribe to per-session execution SSE stream.
 * Invalidates timeline and tool-invocations when events arrive.
 * Captures tool.approval_required for pending approval UI.
 * Tracks current agent activity for loading indicators.
 */
export function useSessionStream(sessionId: string | undefined, enabled: boolean) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<SessionStreamStatus>("connecting")
  const [pendingApproval, setPendingApproval] = useState<PendingToolApproval | null>(null)
  const [activity, setActivity] = useState<AgentActivity>(null)
  const streamUrl = useMemo(
    () => (sessionId ? `${getApiBaseUrl()}/api/sessions/${sessionId}/stream` : null),
    [sessionId],
  )

  const clearPendingApproval = useCallback(() => {
    setPendingApproval(null)
  }, [])

  useEffect(() => {
    if (!streamUrl || !sessionId || !enabled) {
      setStatus("disconnected")
      setPendingApproval(null)
      setActivity(null)
      return
    }

    setActivity({ phase: "thinking" })

    const eventSource = new EventSource(streamUrl)

    eventSource.onopen = () => {
      setStatus("connected")
    }

    eventSource.onerror = () => {
      setStatus("disconnected")
    }

    const invalidateQueries = () => {
      queryClient.invalidateQueries({ queryKey: ["session-timeline", sessionId] })
      queryClient.invalidateQueries({ queryKey: ["session-tool-invocations", sessionId] })
      queryClient.invalidateQueries({ queryKey: ["session-detail", sessionId] })
    }

    const eventTypes = [
      "agent.started",
      "agent.completed",
      "tool.started",
      "tool.completed",
      "tool.approval_required",
      "session.ended",
    ] as const

    const handlers = eventTypes.map((eventType) => {
      const handler = (e: MessageEvent) => {
        let parsed: { payload?: Record<string, unknown> } | undefined
        if (e.data) {
          try {
            parsed = JSON.parse(e.data) as { payload?: Record<string, unknown> }
          } catch {
            // ignore
          }
        }

        if (eventType === "agent.started") {
          setActivity({ phase: "thinking" })
        } else if (eventType === "tool.started") {
          const toolName = (parsed?.payload?.toolName as string) ?? "unknown"
          setActivity({ phase: "tool", toolName })
        } else if (eventType === "tool.completed") {
          setPendingApproval(null)
          setActivity({ phase: "thinking" })
        } else if (eventType === "tool.approval_required") {
          const payload = parsed?.payload as ToolApprovalRequiredPayload | undefined
          if (payload?.invocationId && payload?.toolName) {
            setPendingApproval({
              invocationId: String(payload.invocationId),
              toolName: payload.toolName,
              parameters: payload.parameters,
            })
            setActivity({ phase: "approval", toolName: payload.toolName })
          }
        } else if (eventType === "agent.completed" || eventType === "session.ended") {
          setPendingApproval(null)
          setActivity(null)
        }

        invalidateQueries()
      }
      eventSource.addEventListener(eventType, handler)
      return { eventType, handler }
    })

    return () => {
      handlers.forEach(({ eventType, handler }) =>
        eventSource.removeEventListener(eventType, handler),
      )
      eventSource.close()
    }
  }, [streamUrl, sessionId, enabled, queryClient])

  return { status, pendingApproval, clearPendingApproval, activity }
}
