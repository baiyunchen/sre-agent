import { useEffect, useMemo, useState } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { getApiBaseUrl } from "@/app/lib/api"

export type SessionStreamStatus = "connecting" | "connected" | "disconnected"

/**
 * Subscribe to per-session execution SSE stream.
 * Invalidates timeline and tool-invocations when events arrive.
 */
export function useSessionStream(sessionId: string | undefined, enabled: boolean) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<SessionStreamStatus>("connecting")
  const streamUrl = useMemo(
    () => (sessionId ? `${getApiBaseUrl()}/api/sessions/${sessionId}/stream` : null),
    [sessionId],
  )

  useEffect(() => {
    if (!streamUrl || !sessionId || !enabled) {
      setStatus("disconnected")
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
      "session.ended",
    ] as const

    const handlers = eventTypes.map((eventType) => {
      const handler = () => invalidateQueries()
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

  return { status }
}
