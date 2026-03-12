import { useEffect, useMemo, useState } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { getApiBaseUrl } from "@/app/lib/api"
import type { DashboardSnapshotEvent } from "@/app/lib/types"

export type DashboardStreamStatus = "connecting" | "connected" | "disconnected"

export function useDashboardStream(activeSessionLimit = 8, activityLimit = 12) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<DashboardStreamStatus>("connecting")
  const [lastEventAt, setLastEventAt] = useState<string | null>(null)
  const streamUrl = useMemo(() => `${getApiBaseUrl()}/api/events/stream`, [])

  useEffect(() => {
    const eventSource = new EventSource(streamUrl)

    eventSource.onopen = () => {
      setStatus("connected")
    }

    eventSource.onerror = () => {
      setStatus("disconnected")
    }

    const handleSnapshot = (event: MessageEvent<string>) => {
      try {
        const snapshot = JSON.parse(event.data) as DashboardSnapshotEvent
        if (snapshot.eventType !== "dashboard.snapshot") {
          return
        }

        queryClient.setQueryData(["dashboard-stats"], snapshot.stats)
        queryClient.setQueryData(
          ["dashboard-active-sessions", activeSessionLimit],
          snapshot.activeSessions,
        )
        queryClient.setQueryData(
          ["dashboard-activities", activityLimit],
          snapshot.activities,
        )
        setLastEventAt(snapshot.generatedAt)
      } catch {
        // Keep stream alive even when one payload fails to parse.
      }
    }

    eventSource.addEventListener("dashboard.snapshot", handleSnapshot as EventListener)

    return () => {
      eventSource.removeEventListener("dashboard.snapshot", handleSnapshot as EventListener)
      eventSource.close()
    }
  }, [activeSessionLimit, activityLimit, queryClient, streamUrl])

  return { status, lastEventAt }
}
