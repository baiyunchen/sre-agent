import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import {
  approveSession,
  createApprovalRule,
  deleteApprovalRule,
  fetchApprovalHistory,
  fetchApprovalRules,
  fetchPendingApprovals,
  rejectSession,
} from "@/app/lib/api"
import type { CreateApprovalRuleRequest } from "@/app/lib/types"

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

export function useApprovalRules() {
  return useQuery({
    queryKey: ["approval-rules"],
    queryFn: () => fetchApprovalRules(),
    refetchInterval: 30_000,
  })
}

export function useCreateApprovalRule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateApprovalRuleRequest) => createApprovalRule(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["approval-rules"] })
    },
  })
}

export function useDeleteApprovalRule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteApprovalRule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["approval-rules"] })
    },
  })
}
