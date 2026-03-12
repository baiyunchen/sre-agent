import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import {
  approveSession,
  fetchApprovalHistory,
  fetchPendingApprovals,
  rejectSession,
} from "@/app/lib/api"

export function usePendingApprovals(limit = 20) {
  return useQuery({
    queryKey: ["approvals-pending", limit],
    queryFn: () => fetchPendingApprovals(limit),
    refetchInterval: 15_000,
  })
}

export function useApprovalHistory(limit = 50) {
  return useQuery({
    queryKey: ["approvals-history", limit],
    queryFn: () => fetchApprovalHistory(limit),
    refetchInterval: 20_000,
  })
}

export function useApproveSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ sessionId, approverId, comment }: { sessionId: string; approverId: string; comment?: string }) =>
      approveSession(sessionId, { approverId, comment }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["approvals-pending"] })
      queryClient.invalidateQueries({ queryKey: ["approvals-history"] })
    },
  })
}

export function useRejectSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ sessionId, approverId, comment }: { sessionId: string; approverId: string; comment?: string }) =>
      rejectSession(sessionId, { approverId, comment }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["approvals-pending"] })
      queryClient.invalidateQueries({ queryKey: ["approvals-history"] })
    },
  })
}
