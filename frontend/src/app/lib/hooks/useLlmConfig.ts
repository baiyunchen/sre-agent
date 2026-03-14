import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query"
import { fetchLlmConfig, updateLlmConfig, fetchLlmProviders } from "@/app/lib/api"
import type { LlmConfigUpdateRequest } from "@/app/lib/types"

export function useLlmConfig() {
  return useQuery({
    queryKey: ["llm-config"],
    queryFn: fetchLlmConfig,
    staleTime: 30_000,
  })
}

export function useLlmProviders() {
  return useQuery({
    queryKey: ["llm-providers"],
    queryFn: fetchLlmProviders,
    staleTime: 5 * 60_000,
  })
}

export function useUpdateLlmConfig() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload: LlmConfigUpdateRequest) => updateLlmConfig(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["llm-config"] })
    },
  })
}
