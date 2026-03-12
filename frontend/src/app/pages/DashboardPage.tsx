import { SectionCard } from "@/app/layout/AppLayout"
import {
  useDashboardActivities,
  useDashboardActiveSessions,
  useDashboardStats,
} from "@/app/lib/hooks/useDashboard"
import { useDashboardStream } from "@/app/lib/hooks/useDashboardStream"

function formatDuration(seconds: number): string {
  if (seconds <= 0) {
    return "0s"
  }

  if (seconds < 60) {
    return `${seconds}s`
  }

  const minutes = Math.floor(seconds / 60)
  const remainSeconds = seconds % 60
  return `${minutes}m ${remainSeconds}s`
}

function formatTimestamp(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString()
}

function getConnectionText(status: "connecting" | "connected" | "disconnected"): string {
  if (status === "connected") {
    return "已连接"
  }

  if (status === "disconnected") {
    return "重连中"
  }

  return "连接中"
}

export function DashboardPage() {
  const statsQuery = useDashboardStats()
  const activeSessionsQuery = useDashboardActiveSessions(8)
  const activitiesQuery = useDashboardActivities(12)
  const stream = useDashboardStream(8, 12)

  const stats = statsQuery.data

  return (
    <div className="space-y-4">
      <h2 className="text-2xl font-bold">Dashboard</h2>
      <p className="text-sm text-muted-foreground">
        基于 Figma 设计稿的页面骨架，后续将对接 dashboard stats/activities/active-sessions API。
      </p>
      <p className="text-xs text-muted-foreground">
        实时通道: {getConnectionText(stream.status)}
        {stream.lastEventAt ? ` · 最后更新 ${formatTimestamp(stream.lastEventAt)}` : ""}
      </p>

      {statsQuery.error instanceof Error && (
        <p className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {statsQuery.error.message}
        </p>
      )}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <SectionCard title="Total Sessions Today">
          <p className="text-2xl font-semibold">
            {statsQuery.isLoading ? "--" : (stats?.totalSessionsToday ?? 0)}
          </p>
        </SectionCard>
        <SectionCard title="Auto-Resolution Rate">
          <p className="text-2xl font-semibold">
            {statsQuery.isLoading ? "--" : `${(stats?.autoResolutionRate ?? 0).toFixed(1)}%`}
          </p>
        </SectionCard>
        <SectionCard title="Avg Processing Time">
          <p className="text-2xl font-semibold">
            {statsQuery.isLoading
              ? "--"
              : formatDuration(stats?.avgProcessingTimeSeconds ?? 0)}
          </p>
        </SectionCard>
        <SectionCard title="Pending Approvals">
          <p className="text-2xl font-semibold">
            {statsQuery.isLoading ? "--" : (stats?.pendingApprovals ?? 0)}
          </p>
        </SectionCard>
      </div>

      <SectionCard title="Active Sessions">
        {activeSessionsQuery.isLoading && (
          <p className="text-sm text-muted-foreground">加载活跃会话中...</p>
        )}
        {activeSessionsQuery.error instanceof Error && (
          <p className="text-sm text-destructive">{activeSessionsQuery.error.message}</p>
        )}
        {!activeSessionsQuery.isLoading &&
          !(activeSessionsQuery.error instanceof Error) &&
          (activeSessionsQuery.data?.items.length ?? 0) === 0 && (
            <p className="text-sm text-muted-foreground">当前无活跃会话。</p>
          )}
        {(activeSessionsQuery.data?.items ?? []).map((session) => (
          <div key={session.id} className="mb-2 rounded-md border p-2 text-sm">
            <p className="font-medium">{session.alertName ?? session.id}</p>
            <p className="text-xs text-muted-foreground">
              {session.serviceName ?? "-"} · {session.status} · Step {session.currentStep}
            </p>
          </div>
        ))}
      </SectionCard>

      <SectionCard title="Recent Activities">
        {activitiesQuery.isLoading && (
          <p className="text-sm text-muted-foreground">加载活动流中...</p>
        )}
        {activitiesQuery.error instanceof Error && (
          <p className="text-sm text-destructive">{activitiesQuery.error.message}</p>
        )}
        {!activitiesQuery.isLoading &&
          !(activitiesQuery.error instanceof Error) &&
          (activitiesQuery.data?.items.length ?? 0) === 0 && (
            <p className="text-sm text-muted-foreground">当前无活动记录。</p>
          )}
        {(activitiesQuery.data?.items ?? []).map((item) => (
          <div key={item.id} className="mb-2 rounded-md border p-2 text-sm">
            <p className="font-medium">{item.eventType}</p>
            <p className="text-xs text-muted-foreground">
              {item.description ?? "无描述"} · {item.actor ?? "system"} ·{" "}
              {formatTimestamp(item.occurredAt)}
            </p>
          </div>
        ))}
      </SectionCard>
    </div>
  )
}
