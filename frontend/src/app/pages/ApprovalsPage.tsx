import { useState } from "react"
import {
  useApproveSession,
  useApprovalHistory,
  usePendingApprovals,
  useRejectSession,
} from "@/app/lib/hooks/useApprovals"

function formatTimestamp(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}

export function ApprovalsPage() {
  const [approverId, setApproverId] = useState("oncall-user")
  const [comment, setComment] = useState("")

  const pendingQuery = usePendingApprovals(20)
  const historyQuery = useApprovalHistory(20)
  const approveMutation = useApproveSession()
  const rejectMutation = useRejectSession()

  async function handleApprove(sessionId: string) {
    await approveMutation.mutateAsync({
      sessionId,
      approverId,
      comment: comment.trim() === "" ? undefined : comment.trim(),
    })
  }

  async function handleReject(sessionId: string) {
    await rejectMutation.mutateAsync({
      sessionId,
      approverId,
      comment: comment.trim() === "" ? undefined : comment.trim(),
    })
  }

  return (
    <div className="space-y-3">
      <h2 className="text-2xl font-bold">Approvals</h2>
      <p className="text-sm text-muted-foreground">
        对齐 Figma 的 Approvals 页面，已接入 pending/history/approve/reject API。
      </p>

      <div className="rounded-md border bg-card p-4">
        <h3 className="mb-2 text-sm font-semibold">审批参数</h3>
        <div className="grid gap-2 md:grid-cols-2">
          <label className="text-xs text-muted-foreground">
            Approver ID
            <input
              className="mt-1 w-full rounded-md border bg-background px-2 py-1 text-sm"
              value={approverId}
              onChange={(event) => setApproverId(event.target.value)}
            />
          </label>
          <label className="text-xs text-muted-foreground">
            Comment（可选）
            <input
              className="mt-1 w-full rounded-md border bg-background px-2 py-1 text-sm"
              value={comment}
              onChange={(event) => setComment(event.target.value)}
            />
          </label>
        </div>
      </div>

      {(pendingQuery.error instanceof Error ||
        historyQuery.error instanceof Error ||
        approveMutation.error instanceof Error ||
        rejectMutation.error instanceof Error) && (
        <p className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {pendingQuery.error instanceof Error
            ? pendingQuery.error.message
            : historyQuery.error instanceof Error
              ? historyQuery.error.message
              : approveMutation.error instanceof Error
                ? approveMutation.error.message
                : rejectMutation.error instanceof Error
                  ? rejectMutation.error.message
                  : "审批操作失败"}
        </p>
      )}

      <div className="rounded-md border bg-card p-4">
        <h3 className="mb-2 text-sm font-semibold">
          Pending Approvals ({pendingQuery.data?.total ?? 0})
        </h3>
        {pendingQuery.isLoading && (
          <p className="text-sm text-muted-foreground">加载待审批列表中...</p>
        )}
        {!pendingQuery.isLoading && (pendingQuery.data?.items.length ?? 0) === 0 && (
          <p className="text-sm text-muted-foreground">当前无待审批项。</p>
        )}
        {(pendingQuery.data?.items ?? []).map((item) => (
          <div key={item.sessionId} className="mb-2 rounded-md border p-2 text-sm">
            <p className="font-medium">{item.alertName ?? item.sessionId}</p>
            <p className="text-xs text-muted-foreground">
              {item.serviceName ?? "-"} · {item.status} · {formatTimestamp(item.updatedAt)}
            </p>
            <div className="mt-2 flex gap-2">
              <button
                type="button"
                className="rounded bg-emerald-600 px-2 py-1 text-xs text-white disabled:opacity-60"
                disabled={approveMutation.isPending || rejectMutation.isPending || approverId.trim() === ""}
                onClick={() => handleApprove(item.sessionId)}
              >
                Approve
              </button>
              <button
                type="button"
                className="rounded bg-red-600 px-2 py-1 text-xs text-white disabled:opacity-60"
                disabled={approveMutation.isPending || rejectMutation.isPending || approverId.trim() === ""}
                onClick={() => handleReject(item.sessionId)}
              >
                Reject
              </button>
            </div>
          </div>
        ))}
      </div>

      <div className="rounded-md border bg-card p-4">
        <h3 className="mb-2 text-sm font-semibold">
          Approval History ({historyQuery.data?.total ?? 0})
        </h3>
        {historyQuery.isLoading && (
          <p className="text-sm text-muted-foreground">加载审批历史中...</p>
        )}
        {!historyQuery.isLoading && (historyQuery.data?.items.length ?? 0) === 0 && (
          <p className="text-sm text-muted-foreground">当前无审批历史。</p>
        )}
        {(historyQuery.data?.items ?? []).map((item) => (
          <div key={item.id} className="mb-2 rounded-md border p-2 text-sm">
            <p className="font-medium">
              {item.action} · {item.intervenedBy ?? "unknown"}
            </p>
            <p className="text-xs text-muted-foreground">
              {item.reason ?? "无备注"} · {formatTimestamp(item.intervenedAt)}
            </p>
          </div>
        ))}
      </div>
    </div>
  )
}
