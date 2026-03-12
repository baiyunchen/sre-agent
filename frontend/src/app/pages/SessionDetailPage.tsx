import { useState } from "react"
import type { FormEvent } from "react"
import { useParams } from "react-router-dom"
import { SectionCard } from "@/app/layout/AppLayout"
import { useSessionMessage } from "@/app/lib/hooks/useSessionMessage"

export function SessionDetailPage() {
  const { sessionId } = useParams()
  const [message, setMessage] = useState("")
  const sessionMessageMutation = useSessionMessage(sessionId)

  const canSend = Boolean(sessionId) && message.trim().length > 0 && !sessionMessageMutation.isPending

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!canSend) {
      return
    }

    await sessionMessageMutation.mutateAsync(message.trim())
    setMessage("")
  }

  return (
    <div className="space-y-4">
      <h2 className="text-2xl font-bold">Session Detail</h2>
      <p className="text-sm text-muted-foreground">
        Session ID: <span className="font-mono">{sessionId}</span>
      </p>
      <div className="grid gap-4 lg:grid-cols-2">
        <SectionCard title="Timeline">
          待接入 <code>{`/api/sessions/${sessionId ?? ":id"}/timeline`}</code>
        </SectionCard>
        <SectionCard title="Diagnosis">
          待接入 <code>{`/api/sessions/${sessionId ?? ":id"}/diagnosis`}</code>
        </SectionCard>
        <SectionCard title="Tool Invocations">
          待接入 <code>{`/api/sessions/${sessionId ?? ":id"}/tool-invocations`}</code>
        </SectionCard>
        <SectionCard title="Todos">
          待接入 <code>{`/api/sessions/${sessionId ?? ":id"}/todos`}</code>
        </SectionCard>
      </div>
      <SectionCard title="Send Message">
        <form className="space-y-3" onSubmit={handleSubmit}>
          <textarea
            className="min-h-24 w-full rounded-md border bg-background px-3 py-2 text-sm"
            placeholder="输入你要补充给 Agent 的上下文..."
            value={message}
            onChange={(event) => setMessage(event.target.value)}
          />
          <div className="flex items-center gap-3">
            <button
              className="rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground disabled:cursor-not-allowed disabled:opacity-60"
              type="submit"
              disabled={!canSend}
            >
              {sessionMessageMutation.isPending ? "发送中..." : "发送消息"}
            </button>
            {!sessionId && (
              <p className="text-sm text-destructive">缺少 sessionId，无法发送消息。</p>
            )}
          </div>
        </form>

        {sessionMessageMutation.error instanceof Error && (
          <p className="mt-3 text-sm text-destructive">{sessionMessageMutation.error.message}</p>
        )}

        {sessionMessageMutation.data && (
          <div className="mt-3 space-y-2 rounded-md border bg-muted/20 p-3 text-sm">
            <p className="font-medium">
              Agent 响应：{sessionMessageMutation.data.isSuccess ? "成功" : "失败"}
            </p>
            <p className="whitespace-pre-wrap text-muted-foreground">
              {sessionMessageMutation.data.output ?? sessionMessageMutation.data.error ?? "无返回文本"}
            </p>
            <p className="text-xs text-muted-foreground">
              Token: {sessionMessageMutation.data.tokenUsage.totalTokens} (
              {sessionMessageMutation.data.tokenUsage.promptTokens}/
              {sessionMessageMutation.data.tokenUsage.completionTokens})
            </p>
          </div>
        )}
      </SectionCard>
    </div>
  )
}
