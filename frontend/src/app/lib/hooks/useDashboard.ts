import { useQuery } from "@tanstack/react-query"
import {
  fetchDashboardActiveSessions,
  fetchDashboardStats,
} from "@/app/lib/api"

export function useDashboardStats() {
  return useQuery({
    queryKey: ["dashboard-stats"],
    queryFn: fetchDashboardStats,
    refetchInterval: 15_000,
  })
}

export function useDashboardActiveSessions(limit = 10) {
  return useQuery({
    queryKey: ["dashboard-active-sessions", limit],
    queryFn: () => fetchDashboardActiveSessions(limit),
    refetchInterval: 15_000,
  })
}
