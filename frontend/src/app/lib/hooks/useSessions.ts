import { useQuery } from "@tanstack/react-query"
import { fetchSessions } from "@/app/lib/api"
import type { SessionsQuery } from "@/app/lib/types"

export function useSessions(query: SessionsQuery) {
  return useQuery({
    queryKey: ["sessions", query],
    queryFn: () => fetchSessions(query),
    refetchInterval: 15_000,
  })
}
