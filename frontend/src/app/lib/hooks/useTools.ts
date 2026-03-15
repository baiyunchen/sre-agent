import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { fetchToolRegistry, updateToolApprovalMode } from "@/app/lib/api"

export function useToolRegistry() {
  return useQuery({
    queryKey: ["tool-registry"],
    queryFn: () => fetchToolRegistry(),
    refetchInterval: 30_000,
  })
}

export function useUpdateToolApprovalMode() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      toolName,
      autoApprove,
      updatedBy,
    }: {
      toolName: string
      autoApprove: boolean
      updatedBy?: string
    }) => updateToolApprovalMode(toolName, { autoApprove, updatedBy }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tool-registry"] })
    },
  })
}
