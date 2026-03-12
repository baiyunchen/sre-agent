import { useMemo, useState } from "react"
import { Link } from "react-router-dom"
import { Search, RefreshCw, Filter } from "lucide-react"
import {
  Card,
  CardContent,
} from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import { Progress } from "@/components/ui/progress"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { useSessions } from "@/app/lib/hooks/useSessions"
import type { SessionSummary } from "@/app/lib/types"

function getStatusColor(status: string): string {
  switch (status) {
    case "Running":
      return "bg-blue-500"
    case "Completed":
      return "bg-emerald-500"
    case "Failed":
      return "bg-red-500"
    case "WaitingApproval":
      return "bg-amber-500"
    case "Cancelled":
    case "TimedOut":
      return "bg-gray-500"
    default:
      return "bg-gray-500"
  }
}

function formatDuration(seconds: number | null): string {
  if (seconds === null || seconds <= 0) return "-"
  if (seconds < 60) return `${seconds}s`
  const minutes = Math.floor(seconds / 60)
  const secs = seconds % 60
  if (minutes < 60) return `${minutes}m ${secs}s`
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${mins}m`
}

function formatDate(dateStr: string): string {
  const date = new Date(dateStr)
  if (Number.isNaN(date.getTime())) return dateStr
  const now = new Date()
  const diff = Math.floor((now.getTime() - date.getTime()) / 1000)
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
  return date.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  })
}

export function SessionsPage() {
  const [statusFilter, setStatusFilter] = useState<string>("All")
  const [sourceFilter, setSourceFilter] = useState<string>("All")
  const [searchQuery, setSearchQuery] = useState("")

  const { data, isLoading, error, refetch } = useSessions({
    page: 1,
    pageSize: 50,
    status: statusFilter !== "All" ? statusFilter : undefined,
    source: sourceFilter !== "All" ? sourceFilter : undefined,
    search: searchQuery || undefined,
    sort: "createdAt",
    sortOrder: "desc",
  })

  const items = data?.items ?? []

  const resetFilters = () => {
    setStatusFilter("All")
    setSourceFilter("All")
    setSearchQuery("")
  }

  return (
    <div className="container mx-auto max-w-screen-2xl p-6">
      <div className="flex flex-col gap-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Sessions</h1>
            <p className="text-muted-foreground">
              All alert processing sessions and their status
            </p>
          </div>
          <Button onClick={() => refetch()} className="gap-2">
            <RefreshCw className="size-4" />
            Refresh
          </Button>
        </div>

        {/* Filter Bar */}
        <Card>
          <CardContent className="pt-6">
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
              <div className="relative lg:col-span-2">
                <Search className="absolute left-3 top-3 size-4 text-muted-foreground" />
                <Input
                  placeholder="Search session ID or alert name..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-9"
                />
              </div>

              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger>
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="All">All Statuses</SelectItem>
                  <SelectItem value="Running">Running</SelectItem>
                  <SelectItem value="Completed">Completed</SelectItem>
                  <SelectItem value="Failed">Failed</SelectItem>
                  <SelectItem value="WaitingApproval">Waiting Approval</SelectItem>
                  <SelectItem value="Cancelled">Cancelled</SelectItem>
                  <SelectItem value="TimedOut">Timed Out</SelectItem>
                </SelectContent>
              </Select>

              <Select value={sourceFilter} onValueChange={setSourceFilter}>
                <SelectTrigger>
                  <SelectValue placeholder="Source" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="All">All Sources</SelectItem>
                  <SelectItem value="CloudWatch">CloudWatch</SelectItem>
                  <SelectItem value="Prometheus">Prometheus</SelectItem>
                  <SelectItem value="Slack Manual">Slack Manual</SelectItem>
                </SelectContent>
              </Select>

              {(statusFilter !== "All" || sourceFilter !== "All" || searchQuery) && (
                <Button variant="outline" onClick={resetFilters}>
                  Reset Filters
                </Button>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Sessions Table */}
        <Card>
          <CardContent className="p-0">
            {isLoading && (
              <div className="flex items-center justify-center py-12">
                <p className="text-sm text-muted-foreground">Loading sessions...</p>
              </div>
            )}
            {error instanceof Error && (
              <div className="flex items-center justify-center py-12">
                <p className="text-sm text-destructive">{error.message}</p>
              </div>
            )}
            {!isLoading && !(error instanceof Error) && items.length > 0 && (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Status</TableHead>
                    <TableHead>Alert Name</TableHead>
                    <TableHead>Service</TableHead>
                    <TableHead>Source</TableHead>
                    <TableHead>Started</TableHead>
                    <TableHead>Duration</TableHead>
                    <TableHead className="text-right">Steps</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map((session) => (
                    <TableRow key={session.id} className="cursor-pointer">
                      <TableCell>
                        <Link to={`/sessions/${session.id}`}>
                          <div className="flex items-center gap-2">
                            <div
                              className={`size-2 rounded-full ${getStatusColor(session.status)}`}
                            />
                            <Badge
                              variant="outline"
                              className={session.status === "Running" ? "animate-pulse" : ""}
                            >
                              {session.status}
                            </Badge>
                          </div>
                        </Link>
                      </TableCell>
                      <TableCell>
                        <Link
                          to={`/sessions/${session.id}`}
                          className="hover:underline"
                        >
                          <div className="gap-1">
                            <div className="font-medium">
                              {session.alertName ?? session.id}
                            </div>
                            <div className="font-mono text-xs text-muted-foreground">
                              {session.id}
                            </div>
                          </div>
                        </Link>
                      </TableCell>
                      <TableCell>
                        <span className="font-mono text-sm">
                          {session.serviceName ?? "-"}
                        </span>
                      </TableCell>
                      <TableCell>
                        {session.source ? (
                          <Badge variant="secondary">{session.source}</Badge>
                        ) : (
                          "-"
                        )}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {formatDate(session.createdAt)}
                      </TableCell>
                      <TableCell>
                        <span className="font-mono text-sm">
                          {formatDuration(session.duration)}
                        </span>
                      </TableCell>
                      <TableCell className="text-right font-mono text-sm">
                        {session.agentSteps ?? "-"}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
            {!isLoading && !(error instanceof Error) && items.length === 0 && (
              <div className="flex flex-col items-center justify-center py-12 text-center">
                <Filter className="mb-4 size-12 text-muted-foreground" />
                <h3 className="mb-2 text-lg font-semibold">No sessions found</h3>
                <p className="mb-4 text-sm text-muted-foreground">
                  No sessions match your current filters
                </p>
                <Button variant="outline" onClick={resetFilters}>
                  Reset Filters
                </Button>
              </div>
            )}
          </CardContent>
        </Card>

        {items.length > 0 && (
          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              Showing {items.length} of {data?.total ?? items.length} sessions
            </p>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" disabled>
                Previous
              </Button>
              <Button variant="outline" size="sm" disabled>
                Next
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
