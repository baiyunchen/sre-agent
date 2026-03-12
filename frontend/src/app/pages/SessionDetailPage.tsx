import { useState } from "react"
import type { FormEvent } from "react"
import { useParams } from "react-router-dom"
import { SectionCard } from "@/app/layout/AppLayout"
import {
  useSessionDiagnosis,
  useSessionTimeline,
  useSessionTodos,
  useSessionToolInvocations,
} from "@/app/lib/hooks/useSessionDetailData"
import { useSessionMessage } from "@/app/lib/hooks/useSessionMessage"

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return "-"
  }
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  return date.toLocaleString()
}

export function SessionDetailPage() {
  const { sessionId } = useParams()
  const [message, setMessage] = useState("")
  const timelineQuery = useSessionTimeline(sessionId)
  const diagnosisQuery = useSessionDiagnosis(sessionId)
  const toolInvocationsQuery = useSessionToolInvocations(sessionId)
  const todosQuery = useSessionTodos(sessionId)
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
          {timelineQuery.isLoading && (
            <p className="text-sm text-muted-foreground">加载时间线中...</p>
          )}
          {timelineQuery.error instanceof Error && (
            <p className="text-sm text-destructive">{timelineQuery.error.message}</p>
          )}
          {!timelineQuery.isLoading &&
            !(timelineQuery.error instanceof Error) &&
            (timelineQuery.data?.events.length ?? 0) === 0 && (
              <p className="text-sm text-muted-foreground">暂无时间线事件。</p>
            )}
          {(timelineQuery.data?.events ?? []).slice(0, 12).map((event) => (
            <div key={event.id} className="mb-2 rounded-md border p-2 text-sm">
              <p className="font-medium">{event.title}</p>
              <p className="text-xs text-muted-foreground">
                {event.eventType} · {formatDate(event.timestamp)}
              </p>
              {event.detail && (
                <p className="mt-1 whitespace-pre-wrap text-muted-foreground">{event.detail}</p>
              )}
            </div>
          ))}
        </SectionCard>
        <SectionCard title="Diagnosis">
          {diagnosisQuery.isLoading && (
            <p className="text-sm text-muted-foreground">加载诊断信息中...</p>
          )}
          {diagnosisQuery.error instanceof Error && (
            <p className="text-sm text-destructive">{diagnosisQuery.error.message}</p>
          )}
          {!diagnosisQuery.isLoading &&
            !(diagnosisQuery.error instanceof Error) &&
            diagnosisQuery.data && (
              <div className="space-y-2 text-sm">
                <p className="font-medium">
                  {diagnosisQuery.data.hypothesis || "暂无结构化诊断结论"}
                </p>
                <p className="text-xs text-muted-foreground">
                  Confidence: {diagnosisQuery.data.confidence ?? "-"} · Records:{" "}
                  {diagnosisQuery.data.totalRecords}
                </p>
                {(diagnosisQuery.data.recommendedActions ?? []).length > 0 && (
                  <div>
                    <p className="text-xs font-medium text-muted-foreground">Recommended Actions</p>
                    {(diagnosisQuery.data.recommendedActions ?? []).slice(0, 3).map((action) => (
                      <p key={action} className="text-muted-foreground">
                        - {action}
                      </p>
                    ))}
                  </div>
                )}
              </div>
            )}
        </SectionCard>
        <SectionCard title="Tool Invocations">
          {toolInvocationsQuery.isLoading && (
            <p className="text-sm text-muted-foreground">加载工具调用中...</p>
          )}
          {toolInvocationsQuery.error instanceof Error && (
            <p className="text-sm text-destructive">{toolInvocationsQuery.error.message}</p>
          )}
          {!toolInvocationsQuery.isLoading &&
            !(toolInvocationsQuery.error instanceof Error) &&
            (toolInvocationsQuery.data?.items.length ?? 0) === 0 && (
              <p className="text-sm text-muted-foreground">暂无工具调用。</p>
            )}
          {(toolInvocationsQuery.data?.items ?? []).slice(0, 8).map((item) => (
            <div key={item.id} className="mb-2 rounded-md border p-2 text-sm">
              <p className="font-medium">
                {item.toolName} · {item.status}
              </p>
              <p className="text-xs text-muted-foreground">
                {formatDate(item.requestedAt)} · {item.durationMs}ms
              </p>
              {item.errorMessage && <p className="mt-1 text-destructive">{item.errorMessage}</p>}
            </div>
          ))}
        </SectionCard>
        <SectionCard title="Todos">
          {todosQuery.isLoading && (
            <p className="text-sm text-muted-foreground">加载 Todo 中...</p>
          )}
          {todosQuery.error instanceof Error && (
            <p className="text-sm text-destructive">{todosQuery.error.message}</p>
          )}
          {!todosQuery.isLoading &&
            !(todosQuery.error instanceof Error) &&
            (todosQuery.data?.items.length ?? 0) === 0 && (
              <p className="text-sm text-muted-foreground">暂无 Todo。</p>
            )}
          {(todosQuery.data?.items ?? []).slice(0, 8).map((todo) => (
            <div key={todo.id} className="mb-2 rounded-md border p-2 text-sm">
              <p className="font-medium">{todo.content}</p>
              <p className="text-xs text-muted-foreground">
                {todo.status} · {todo.priority} · {formatDate(todo.updatedAt)}
              </p>
            </div>
          ))}
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
