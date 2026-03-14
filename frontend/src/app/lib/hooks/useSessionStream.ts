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

/**
 * Subscribe to per-session execution SSE stream.
 * Invalidates timeline and tool-invocations when events arrive.
 * Captures tool.approval_required for pending approval UI.
 */
export function useSessionStream(sessionId: string | undefined, enabled: boolean) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<SessionStreamStatus>("connecting")
  const [pendingApproval, setPendingApproval] = useState<PendingToolApproval | null>(null)
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
      return
    }

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
        if (eventType === "tool.approval_required" && e.data) {
          try {
            const data = JSON.parse(e.data) as { payload?: ToolApprovalRequiredPayload }
            const payload = data.payload
            if (payload?.invocationId && payload?.toolName) {
              setPendingApproval({
                invocationId: String(payload.invocationId),
                toolName: payload.toolName,
                parameters: payload.parameters,
              })
            }
          } catch {
            // ignore parse errors
          }
        } else if (eventType === "tool.completed") {
          setPendingApproval(null)
        } else if (eventType === "session.ended") {
          setPendingApproval(null)
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

  return { status, pendingApproval, clearPendingApproval }
}
