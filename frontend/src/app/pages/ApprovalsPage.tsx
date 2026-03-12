import { useState } from "react"
import { Link } from "react-router-dom"
import {
  Clock,
  CheckCircle2,
  XCircle,
  Shield,
  Filter,
  Plus,
  Trash2,
} from "lucide-react"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  useApproveSession,
  useApprovalHistory,
  useApprovalRules,
  useCreateApprovalRule,
  useDeleteApprovalRule,
  usePendingApprovals,
  useRejectSession,
} from "@/app/lib/hooks/useApprovals"

function formatTimestamp(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleString()
}

export function ApprovalsPage() {
  const [approverId, setApproverId] = useState("oncall-user")
  const [comment, setComment] = useState("")
  const [historyFilter, setHistoryFilter] = useState<string>("all")
  const [newRuleToolName, setNewRuleToolName] = useState("")
  const [newRuleType, setNewRuleType] = useState<"always-allow" | "always-deny">("always-allow")

  const pendingQuery = usePendingApprovals(20)
  const historyQuery = useApprovalHistory(50)
  const approveMutation = useApproveSession()
  const rejectMutation = useRejectSession()
  const rulesQuery = useApprovalRules()
  const createRuleMutation = useCreateApprovalRule()
  const deleteRuleMutation = useDeleteApprovalRule()

  const historyItems = historyQuery.data?.items ?? []
  const filteredHistory =
    historyFilter === "all"
      ? historyItems
      : historyItems.filter((h) => h.action.toLowerCase() === historyFilter)

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

  const isMutating = approveMutation.isPending || rejectMutation.isPending

  return (
    <div className="container mx-auto max-w-screen-2xl p-6">
      <div className="flex flex-col gap-6">
        <div>
          <h1 className="text-3xl font-bold">Approval Management</h1>
          <p className="text-muted-foreground">
            Review and manage tool execution approvals
          </p>
        </div>

        {/* Approver Config */}
        <Card>
          <CardContent className="pt-6">
            <div className="grid gap-4 md:grid-cols-2">
              <div className="flex flex-col gap-2">
                <Label htmlFor="approver-id">Approver ID</Label>
                <Input
                  id="approver-id"
                  value={approverId}
                  onChange={(e) => setApproverId(e.target.value)}
                  placeholder="e.g., oncall-user"
                />
              </div>
              <div className="flex flex-col gap-2">
                <Label htmlFor="comment">Comment (optional)</Label>
                <Input
                  id="comment"
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                  placeholder="Optional comment..."
                />
              </div>
            </div>
          </CardContent>
        </Card>

        {(approveMutation.error instanceof Error ||
          rejectMutation.error instanceof Error) && (
          <p className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {approveMutation.error instanceof Error
              ? approveMutation.error.message
              : (rejectMutation.error as Error).message}
          </p>
        )}

        <Tabs defaultValue="pending" className="flex flex-col gap-4">
          <TabsList>
            <TabsTrigger value="pending" className="gap-2">
              Pending
              {(pendingQuery.data?.total ?? 0) > 0 && (
                <Badge
                  variant="destructive"
                  className="ml-1 size-5 rounded-full p-0 text-xs"
                >
                  {pendingQuery.data?.total}
                </Badge>
              )}
            </TabsTrigger>
            <TabsTrigger value="history">History</TabsTrigger>
            <TabsTrigger value="rules" className="gap-2">
              <Shield className="size-4" />
              Rules
              {(rulesQuery.data?.total ?? 0) > 0 && (
                <Badge variant="outline" className="ml-1 text-xs">
                  {rulesQuery.data?.total}
                </Badge>
              )}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="pending" className="flex flex-col gap-4">
            {pendingQuery.isLoading && (
              <p className="text-sm text-muted-foreground">Loading pending approvals...</p>
            )}
            {pendingQuery.error instanceof Error && (
              <p className="text-sm text-destructive">{pendingQuery.error.message}</p>
            )}
            {!pendingQuery.isLoading &&
              !(pendingQuery.error instanceof Error) &&
              (pendingQuery.data?.items.length ?? 0) === 0 && (
                <Card>
                  <CardContent className="flex flex-col items-center justify-center py-16">
                    <CheckCircle2 className="mb-4 size-16 text-emerald-500" />
                    <h3 className="text-xl font-semibold">
                      No pending approvals — all clear!
                    </h3>
                    <p className="text-sm text-muted-foreground">
                      All tool execution requests have been handled
                    </p>
                  </CardContent>
                </Card>
              )}
            <div className="grid gap-4">
              {(pendingQuery.data?.items ?? []).map((approval) => (
                <Card key={approval.sessionId}>
                  <CardHeader>
                    <div className="flex items-start justify-between">
                      <div>
                        <CardTitle className="text-lg">
                          {approval.alertName ?? approval.sessionId}
                        </CardTitle>
                        <CardDescription className="flex items-center gap-2">
                          {approval.serviceName ?? "-"} ·{" "}
                          <Badge variant="outline">{approval.status}</Badge>
                        </CardDescription>
                      </div>
                      <div className="flex items-center gap-2 text-sm text-muted-foreground">
                        <Clock className="size-4" />
                        <span>{formatTimestamp(approval.updatedAt)}</span>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-4">
                    <div>
                      <Label className="text-xs text-muted-foreground">Session</Label>
                      <Link
                        to={`/sessions/${approval.sessionId}`}
                        className="block font-mono text-sm text-blue-500 hover:underline"
                      >
                        {approval.sessionId}
                      </Link>
                    </div>

                    <div className="flex flex-wrap gap-2">
                      <Button
                        onClick={() => handleApprove(approval.sessionId)}
                        disabled={isMutating || approverId.trim() === ""}
                        className="bg-emerald-600 hover:bg-emerald-700"
                      >
                        <CheckCircle2 className="size-4" />
                        Approve
                      </Button>
                      <Button
                        variant="destructive"
                        onClick={() => handleReject(approval.sessionId)}
                        disabled={isMutating || approverId.trim() === ""}
                      >
                        <XCircle className="size-4" />
                        Reject
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          </TabsContent>

          <TabsContent value="history" className="flex flex-col gap-4">
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle>Approval History</CardTitle>
                    <CardDescription>
                      Past approval decisions
                    </CardDescription>
                  </div>
                  <Select value={historyFilter} onValueChange={setHistoryFilter}>
                    <SelectTrigger className="w-[180px]">
                      <Filter className="size-4" />
                      <SelectValue placeholder="Filter" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Decisions</SelectItem>
                      <SelectItem value="approve">Approved</SelectItem>
                      <SelectItem value="reject">Rejected</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </CardHeader>
              <CardContent>
                {historyQuery.isLoading && (
                  <p className="text-sm text-muted-foreground">Loading history...</p>
                )}
                {historyQuery.error instanceof Error && (
                  <p className="text-sm text-destructive">{historyQuery.error.message}</p>
                )}
                {!historyQuery.isLoading &&
                  !(historyQuery.error instanceof Error) &&
                  filteredHistory.length === 0 && (
                    <p className="py-8 text-center text-sm text-muted-foreground">
                      No approval history yet.
                    </p>
                  )}
                {filteredHistory.length > 0 && (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Action</TableHead>
                        <TableHead>Session</TableHead>
                        <TableHead>Decision</TableHead>
                        <TableHead>Decided By</TableHead>
                        <TableHead>Decided At</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {filteredHistory.map((item) => (
                        <TableRow key={item.id}>
                          <TableCell className="font-mono text-sm">
                            {item.action}
                          </TableCell>
                          <TableCell>
                            <Link
                              to={`/sessions/${item.sessionId}`}
                              className="font-mono text-sm text-blue-500 hover:underline"
                            >
                              {item.sessionId.slice(0, 12)}...
                            </Link>
                          </TableCell>
                          <TableCell>
                            <Badge
                              variant={
                                item.action === "Approve" ? "default" : "destructive"
                              }
                            >
                              {item.action === "Approve" && (
                                <CheckCircle2 className="mr-1 size-3" />
                              )}
                              {item.action === "Reject" && (
                                <XCircle className="mr-1 size-3" />
                              )}
                              {item.action}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-sm">
                            {item.intervenedBy ?? "unknown"}
                          </TableCell>
                          <TableCell className="text-sm text-muted-foreground">
                            {formatTimestamp(item.intervenedAt)}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="rules" className="flex flex-col gap-4">
            <Card>
              <CardHeader>
                <CardTitle>Permanent Approval Rules</CardTitle>
                <CardDescription>
                  Rules that auto-approve or auto-deny specific tool executions
                </CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-4">
                <div className="flex items-end gap-3">
                  <div className="flex flex-col gap-2">
                    <Label htmlFor="rule-tool">Tool Name</Label>
                    <Input
                      id="rule-tool"
                      value={newRuleToolName}
                      onChange={(e) => setNewRuleToolName(e.target.value)}
                      placeholder="e.g., kubectl_delete_pod"
                      className="w-64"
                    />
                  </div>
                  <div className="flex flex-col gap-2">
                    <Label htmlFor="rule-type">Rule Type</Label>
                    <Select
                      value={newRuleType}
                      onValueChange={(v) =>
                        setNewRuleType(v as "always-allow" | "always-deny")
                      }
                    >
                      <SelectTrigger className="w-[160px]" id="rule-type">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="always-allow">Always Allow</SelectItem>
                        <SelectItem value="always-deny">Always Deny</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <Button
                    onClick={async () => {
                      if (!newRuleToolName.trim()) return
                      await createRuleMutation.mutateAsync({
                        toolName: newRuleToolName.trim(),
                        ruleType: newRuleType,
                        createdBy: approverId || undefined,
                      })
                      setNewRuleToolName("")
                    }}
                    disabled={
                      !newRuleToolName.trim() || createRuleMutation.isPending
                    }
                  >
                    <Plus className="size-4" />
                    Add Rule
                  </Button>
                </div>

                {rulesQuery.isLoading && (
                  <p className="text-sm text-muted-foreground">Loading rules...</p>
                )}
                {rulesQuery.error instanceof Error && (
                  <p className="text-sm text-destructive">
                    {rulesQuery.error.message}
                  </p>
                )}
                {!rulesQuery.isLoading &&
                  !(rulesQuery.error instanceof Error) &&
                  (rulesQuery.data?.items.length ?? 0) === 0 && (
                    <p className="py-8 text-center text-sm text-muted-foreground">
                      No approval rules configured yet.
                    </p>
                  )}
                {(rulesQuery.data?.items.length ?? 0) > 0 && (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Tool Name</TableHead>
                        <TableHead>Rule Type</TableHead>
                        <TableHead>Created By</TableHead>
                        <TableHead>Created At</TableHead>
                        <TableHead className="w-16" />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {(rulesQuery.data?.items ?? []).map((rule) => (
                        <TableRow key={rule.id}>
                          <TableCell className="font-mono text-sm">
                            {rule.toolName}
                          </TableCell>
                          <TableCell>
                            <Badge
                              variant={
                                rule.ruleType === "always-allow"
                                  ? "default"
                                  : "destructive"
                              }
                            >
                              {rule.ruleType === "always-allow" && (
                                <CheckCircle2 className="mr-1 size-3" />
                              )}
                              {rule.ruleType === "always-deny" && (
                                <XCircle className="mr-1 size-3" />
                              )}
                              {rule.ruleType}
                            </Badge>
                          </TableCell>
                          <TableCell className="text-sm">
                            {rule.createdBy ?? "-"}
                          </TableCell>
                          <TableCell className="text-sm text-muted-foreground">
                            {formatTimestamp(rule.createdAt)}
                          </TableCell>
                          <TableCell>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() =>
                                deleteRuleMutation.mutateAsync(rule.id)
                              }
                              disabled={deleteRuleMutation.isPending}
                            >
                              <Trash2 className="size-4 text-destructive" />
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
    </div>
  )
}
