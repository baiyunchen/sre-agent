import { Link } from "react-router-dom"
import {
  TrendingUp,
  TrendingDown,
  Clock,
  CheckCircle2,
  AlertCircle,
  Activity,
  Circle,
} from "lucide-react"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Progress } from "@/components/ui/progress"
import { Separator } from "@/components/ui/separator"
import {
  useDashboardActivities,
  useDashboardActiveSessions,
  useDashboardStats,
} from "@/app/lib/hooks/useDashboard"
import { useDashboardStream } from "@/app/lib/hooks/useDashboardStream"

function formatDuration(seconds: number): string {
  if (seconds <= 0) return "0s"
  if (seconds < 60) return `${seconds}s`
  const minutes = Math.floor(seconds / 60)
  const secs = seconds % 60
  if (minutes < 60) return `${minutes}m ${secs}s`
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${mins}m`
}

function formatTimestamp(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const now = new Date()
  const diff = Math.floor((now.getTime() - date.getTime()) / 1000)
  if (diff < 60) return `${diff}s ago`
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
  return date.toLocaleDateString()
}

export function DashboardPage() {
  const statsQuery = useDashboardStats()
  const activeSessionsQuery = useDashboardActiveSessions(8)
  const activitiesQuery = useDashboardActivities(12)
  useDashboardStream(8, 12)

  const stats = statsQuery.data

  return (
    <div className="container mx-auto max-w-screen-2xl p-6">
      <div className="flex flex-col gap-6">
        {/* Top Stats Row */}
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Total Sessions Today</CardTitle>
              <Activity className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {statsQuery.isLoading ? "--" : (stats?.totalSessionsToday ?? 0)}
              </div>
              <p className="flex items-center gap-1 text-xs text-muted-foreground">
                <TrendingUp className="size-3 text-emerald-500" />
                <span className="text-emerald-500">live</span> from API
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Auto-Resolution Rate</CardTitle>
              <CheckCircle2 className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {statsQuery.isLoading
                  ? "--"
                  : `${(stats?.autoResolutionRate ?? 0).toFixed(0)}%`}
              </div>
              <div className="mt-2">
                <Progress value={stats?.autoResolutionRate ?? 0} className="h-2" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Avg Processing Time</CardTitle>
              <Clock className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {statsQuery.isLoading
                  ? "--"
                  : formatDuration(stats?.avgProcessingTimeSeconds ?? 0)}
              </div>
              <p className="flex items-center gap-1 text-xs text-muted-foreground">
                <TrendingDown className="size-3 text-emerald-500" />
                <span className="text-emerald-500">optimizing</span>
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Pending Approvals</CardTitle>
              <AlertCircle className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {statsQuery.isLoading ? "--" : (stats?.pendingApprovals ?? 0)}
              </div>
              {(stats?.pendingApprovals ?? 0) > 0 && (
                <Badge variant="outline" className="mt-2 border-amber-500 text-amber-500">
                  Requires attention
                </Badge>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="grid gap-6 lg:grid-cols-3">
          {/* Real-time Activity Feed */}
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle>Real-time Activity Feed</CardTitle>
              <CardDescription>Live updates from active sessions</CardDescription>
            </CardHeader>
            <CardContent>
              {activitiesQuery.isLoading && (
                <p className="text-sm text-muted-foreground">Loading activities...</p>
              )}
              {activitiesQuery.error instanceof Error && (
                <p className="text-sm text-destructive">{activitiesQuery.error.message}</p>
              )}
              {!activitiesQuery.isLoading &&
                !(activitiesQuery.error instanceof Error) &&
                (activitiesQuery.data?.items.length ?? 0) === 0 && (
                  <div className="flex flex-col items-center justify-center py-8 text-center">
                    <CheckCircle2 className="mb-2 size-12 text-muted-foreground" />
                    <p className="text-sm font-medium">No recent activities</p>
                    <p className="text-xs text-muted-foreground">All quiet</p>
                  </div>
                )}
              <div className="flex flex-col gap-1">
                {(activitiesQuery.data?.items ?? []).map((activity) => (
                  <Link
                    key={activity.id}
                    to={`/sessions/${activity.sessionId}`}
                    className="block"
                  >
                    <div className="flex items-start gap-4 rounded-lg p-3 transition-colors hover:bg-accent">
                      <div className="flex size-8 shrink-0 items-center justify-center rounded-full bg-muted">
                        <Circle className="size-4" />
                      </div>
                      <div className="flex-1 gap-1">
                        <div className="flex items-center justify-between gap-2">
                          <p className="text-sm font-medium">
                            {activity.description ?? activity.eventType}
                          </p>
                        </div>
                        <div className="flex items-center gap-2 text-xs text-muted-foreground">
                          <span className="font-mono">{activity.sessionId.slice(0, 12)}</span>
                          <span>·</span>
                          <span>{activity.actor ?? "system"}</span>
                          <span>·</span>
                          <span>{formatTimestamp(activity.occurredAt)}</span>
                        </div>
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            </CardContent>
          </Card>

          {/* Active Sessions Panel */}
          <Card>
            <CardHeader>
              <CardTitle>Active Sessions</CardTitle>
              <CardDescription>Currently running diagnostics</CardDescription>
            </CardHeader>
            <CardContent>
              {activeSessionsQuery.isLoading && (
                <p className="text-sm text-muted-foreground">Loading...</p>
              )}
              {activeSessionsQuery.error instanceof Error && (
                <p className="text-sm text-destructive">{activeSessionsQuery.error.message}</p>
              )}
              {!activeSessionsQuery.isLoading &&
                !(activeSessionsQuery.error instanceof Error) &&
                (activeSessionsQuery.data?.items.length ?? 0) === 0 && (
                  <div className="flex flex-col items-center justify-center py-8 text-center">
                    <CheckCircle2 className="mb-2 size-12 text-muted-foreground" />
                    <p className="text-sm font-medium">No active sessions</p>
                    <p className="text-xs text-muted-foreground">All quiet</p>
                  </div>
                )}
              <div className="flex flex-col gap-4">
                {(activeSessionsQuery.data?.items ?? []).map((session) => (
                  <Link
                    key={session.id}
                    to={`/sessions/${session.id}`}
                    className="block"
                  >
                    <div className="flex flex-col gap-3 rounded-lg border p-3 transition-colors hover:bg-accent">
                      <div className="gap-1">
                        <p className="text-sm font-medium">
                          {session.alertName ?? session.id}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {session.serviceName ?? "-"}
                        </p>
                      </div>
                      <div className="gap-1">
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-muted-foreground">
                            Step {session.currentStep}
                          </span>
                          <Badge variant="outline" className="text-xs">
                            {session.status}
                          </Badge>
                        </div>
                        <Progress value={session.currentStep * 15} className="mt-1 h-1.5" />
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
