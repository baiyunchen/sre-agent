import { useQuery } from "@tanstack/react-query"
import {
  fetchSessionDetail,
  fetchSessionDiagnosis,
  fetchSessionTimeline,
  fetchSessionTodos,
  fetchSessionToolInvocations,
} from "@/app/lib/api"

const FAST_POLL = 3_000
const SLOW_POLL = 30_000

function pollInterval(isRunning: boolean, fast = FAST_POLL, slow = SLOW_POLL) {
  return isRunning ? fast : slow
}

export function useSessionDetail(sessionId: string | undefined, isRunning = false) {
  return useQuery({
    queryKey: ["session-detail", sessionId],
    queryFn: () => fetchSessionDetail(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: pollInterval(isRunning, 5_000, SLOW_POLL),
  })
}

export function useSessionTimeline(sessionId: string | undefined, isRunning = false) {
  return useQuery({
    queryKey: ["session-timeline", sessionId],
    queryFn: () => fetchSessionTimeline(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: pollInterval(isRunning),
  })
}

export function useSessionDiagnosis(sessionId: string | undefined, isRunning = false) {
  return useQuery({
    queryKey: ["session-diagnosis", sessionId],
    queryFn: () => fetchSessionDiagnosis(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: pollInterval(isRunning, 10_000, SLOW_POLL),
  })
}

export function useSessionToolInvocations(sessionId: string | undefined, isRunning = false) {
  return useQuery({
    queryKey: ["session-tool-invocations", sessionId],
    queryFn: () => fetchSessionToolInvocations(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: pollInterval(isRunning),
  })
}

export function useSessionTodos(sessionId: string | undefined, isRunning = false) {
  return useQuery({
    queryKey: ["session-todos", sessionId],
    queryFn: () => fetchSessionTodos(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: pollInterval(isRunning),
  })
}
