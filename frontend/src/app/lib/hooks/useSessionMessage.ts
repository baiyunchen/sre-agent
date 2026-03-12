import { useMutation } from "@tanstack/react-query"
import { postSessionMessage } from "@/app/lib/api"

export function useSessionMessage(sessionId: string | undefined) {
  return useMutation({
    mutationFn: async (message: string) => {
      if (!sessionId) {
        throw new Error("缺少 sessionId，无法发送消息")
      }

      return postSessionMessage(sessionId, { message })
    },
  })
}
