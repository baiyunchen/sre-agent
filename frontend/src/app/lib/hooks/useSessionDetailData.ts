import { useQuery } from "@tanstack/react-query"
import {
  fetchSessionDetail,
  fetchSessionDiagnosis,
  fetchSessionTimeline,
  fetchSessionTodos,
  fetchSessionToolInvocations,
} from "@/app/lib/api"

export function useSessionDetail(sessionId: string | undefined) {
  return useQuery({
    queryKey: ["session-detail", sessionId],
    queryFn: () => fetchSessionDetail(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: 30_000,
  })
}

export function useSessionTimeline(sessionId: string | undefined) {
  return useQuery({
    queryKey: ["session-timeline", sessionId],
    queryFn: () => fetchSessionTimeline(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: 15_000,
  })
}

export function useSessionDiagnosis(sessionId: string | undefined) {
  return useQuery({
    queryKey: ["session-diagnosis", sessionId],
    queryFn: () => fetchSessionDiagnosis(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: 30_000,
  })
}

export function useSessionToolInvocations(sessionId: string | undefined) {
  return useQuery({
    queryKey: ["session-tool-invocations", sessionId],
    queryFn: () => fetchSessionToolInvocations(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: 15_000,
  })
}

export function useSessionTodos(sessionId: string | undefined) {
  return useQuery({
    queryKey: ["session-todos", sessionId],
    queryFn: () => fetchSessionTodos(sessionId as string),
    enabled: Boolean(sessionId),
    refetchInterval: 15_000,
  })
}
