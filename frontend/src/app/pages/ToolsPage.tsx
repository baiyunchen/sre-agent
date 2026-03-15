import { useMemo, useState } from "react"
import { Activity, Clock, Shield, TrendingUp } from "lucide-react"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Switch } from "@/components/ui/switch"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useToolRegistry, useUpdateToolApprovalMode } from "@/app/lib/hooks/useTools"

interface Agent {
  id: string
  name: string
  role: string
  modelProvider: string
  modelName: string
  tools: string[]
  sessionsHandled: number
  avgConfidence: number
  avgDuration: string
  isActive: boolean
}

const staticAgents: Agent[] = [
  {
    id: "agent-001",
    name: "Diagnostic Agent",
    role: "Primary incident diagnosis and root cause analysis",
    modelProvider: "Aliyun Bailian",
    modelName: "qwen-plus",
    tools: [
      "cloudwatch_query_logs",
      "cloudwatch_get_metrics",
      "kubectl_get_logs",
      "query_knowledge_base",
      "run_diagnostic_check",
    ],
    sessionsHandled: 234,
    avgConfidence: 87.3,
    avgDuration: "2m 34s",
    isActive: true,
  },
  {
    id: "agent-002",
    name: "Recovery Agent",
    role: "Automated remediation and service recovery",
    modelProvider: "Zhipu AI",
    modelName: "glm-4",
    tools: ["kubectl_delete_pod", "kubectl_get_logs", "create_todo_ticket"],
    sessionsHandled: 89,
    avgConfidence: 92.1,
    avgDuration: "1m 45s",
    isActive: true,
  },
  {
    id: "agent-003",
    name: "Knowledge Agent",
    role: "Historical incident analysis and pattern recognition",
    modelProvider: "Aliyun Bailian",
    modelName: "qwen-turbo",
    tools: ["query_knowledge_base", "search_slack_history"],
    sessionsHandled: 156,
    avgConfidence: 79.5,
    avgDuration: "1m 12s",
    isActive: true,
  },
]

function formatDuration(durationMs: number): string {
  if (durationMs <= 0) return "-"
  if (durationMs < 1000) return `${durationMs}ms`
  return `${(durationMs / 1000).toFixed(1)}s`
}

export function ToolsPage() {
  const [agents, setAgents] = useState<Agent[]>(staticAgents)
  const [searchQuery, setSearchQuery] = useState("")
  const [operatorId, setOperatorId] = useState("tools-admin")
  const registryQuery = useToolRegistry()
  const updateApprovalModeMutation = useUpdateToolApprovalMode()

  const toggleAgentStatus = (id: string) => {
    setAgents((prev) => prev.map((a) => (a.id === id ? { ...a, isActive: !a.isActive } : a)))
  }

  const filteredTools = useMemo(
    () =>
      (registryQuery.data?.items ?? []).filter(
        (tool) =>
          tool.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          tool.summary.toLowerCase().includes(searchQuery.toLowerCase()) ||
          tool.category.toLowerCase().includes(searchQuery.toLowerCase()),
      ),
    [registryQuery.data?.items, searchQuery],
  )

  const filteredAgents = agents.filter(
    (agent) =>
      agent.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      agent.role.toLowerCase().includes(searchQuery.toLowerCase()),
  )

  return (
    <div className="container mx-auto max-w-screen-2xl p-6">
      <div className="flex flex-col gap-6">
        <div>
          <h1 className="text-3xl font-bold">Tools & Agents Registry</h1>
          <p className="text-muted-foreground">
            Manage backend tool registry and per-tool auto approval
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-4">
          <Input
            placeholder="Search tools and agents..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="max-w-sm"
          />
          <div className="flex flex-col gap-1">
            <Label htmlFor="tools-operator" className="text-xs text-muted-foreground">
              Operator ID (optional)
            </Label>
            <Input
              id="tools-operator"
              value={operatorId}
              onChange={(e) => setOperatorId(e.target.value)}
              placeholder="e.g., oncall-user"
              className="w-52"
            />
          </div>
        </div>

        <Tabs defaultValue="tools" className="flex flex-col gap-4">
          <TabsList>
            <TabsTrigger value="tools">
              Tools ({registryQuery.data?.total ?? 0})
            </TabsTrigger>
            <TabsTrigger value="agents">Agents ({agents.length})</TabsTrigger>
          </TabsList>

          <TabsContent value="tools" className="flex flex-col gap-4">
            {registryQuery.isLoading && (
              <p className="text-sm text-muted-foreground">Loading tools from backend...</p>
            )}
            {registryQuery.error instanceof Error && (
              <p className="text-sm text-destructive">{registryQuery.error.message}</p>
            )}
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
              {filteredTools.map((tool) => {
                const isMutatingCurrentTool =
                  updateApprovalModeMutation.isPending &&
                  updateApprovalModeMutation.variables?.toolName === tool.name
                return (
                  <Card key={tool.name}>
                    <CardHeader>
                      <div className="flex items-start justify-between">
                        <div className="flex items-start gap-3">
                          <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-muted">
                            <Activity className="size-5" />
                          </div>
                          <div className="min-w-0 flex-1">
                            <CardTitle className="font-mono text-base">{tool.name}</CardTitle>
                            <div className="mt-1 flex flex-wrap items-center gap-2">
                              <Badge variant="outline">
                                {tool.category}
                              </Badge>
                              {tool.approvalMode !== "auto-approve" && (
                                <Badge
                                  variant="outline"
                                  className="border-amber-500 text-amber-500"
                                >
                                  <Shield className="size-3" />
                                  {tool.approvalMode}
                                </Badge>
                              )}
                            </div>
                          </div>
                        </div>
                      </div>
                    </CardHeader>
                    <CardContent className="flex flex-col gap-4">
                      <p className="text-sm text-muted-foreground">{tool.summary}</p>

                      <div className="grid grid-cols-3 gap-2 text-center">
                        <div>
                          <div className="text-lg font-bold">{tool.invocations}</div>
                          <div className="text-xs text-muted-foreground">Invocations</div>
                        </div>
                        <div>
                          <div className="flex items-center justify-center gap-1 text-lg font-bold text-emerald-500">
                            {tool.successRate}%
                          </div>
                          <div className="text-xs text-muted-foreground">Success</div>
                        </div>
                        <div>
                          <div className="font-mono text-sm font-bold">
                            {formatDuration(tool.avgDurationMs)}
                          </div>
                          <div className="text-xs text-muted-foreground">Avg Time</div>
                        </div>
                      </div>

                      <div className="flex items-center justify-between border-t pt-4">
                        <div className="flex items-center gap-2">
                          <span className="text-sm">
                            {tool.approvalMode === "always-deny"
                              ? "Always Deny"
                              : tool.autoApprove
                                ? "Auto Approve"
                                : "Require Approval"}
                          </span>
                        </div>
                        <Switch
                          checked={tool.autoApprove}
                          disabled={isMutatingCurrentTool}
                          onCheckedChange={(checked) =>
                            updateApprovalModeMutation.mutate({
                              toolName: tool.name,
                              autoApprove: checked,
                              updatedBy:
                                operatorId.trim() === "" ? undefined : operatorId.trim(),
                            })
                          }
                        />
                      </div>
                    </CardContent>
                  </Card>
                )
              })}
            </div>
            {!registryQuery.isLoading &&
              !(registryQuery.error instanceof Error) &&
              filteredTools.length === 0 && (
                <p className="py-8 text-center text-sm text-muted-foreground">
                  No tools found.
                </p>
              )}
          </TabsContent>

          <TabsContent value="agents" className="flex flex-col gap-4">
            <div className="grid gap-4 md:grid-cols-2">
              {filteredAgents.map((agent) => (
                <Card key={agent.id} className={!agent.isActive ? "opacity-60" : ""}>
                  <CardHeader>
                    <div className="flex items-start justify-between">
                      <div>
                        <CardTitle className="text-xl">{agent.name}</CardTitle>
                        <CardDescription className="mt-1">{agent.role}</CardDescription>
                      </div>
                      <div
                        className={`size-3 rounded-full ${agent.isActive ? "bg-emerald-500" : "bg-gray-500"}`}
                      />
                    </div>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-4">
                    <div className="flex flex-col gap-2">
                      <div className="flex items-center justify-between text-sm">
                        <span className="text-muted-foreground">Model Provider</span>
                        <Badge variant="outline">{agent.modelProvider}</Badge>
                      </div>
                      <div className="flex items-center justify-between text-sm">
                        <span className="text-muted-foreground">Model</span>
                        <span className="font-mono text-sm">{agent.modelName}</span>
                      </div>
                    </div>

                    <div>
                      <Label className="mb-2 text-xs text-muted-foreground">
                        Available Tools
                      </Label>
                      <div className="flex flex-wrap gap-1">
                        {agent.tools.map((toolName) => (
                          <Badge key={toolName} variant="secondary" className="text-xs">
                            {toolName}
                          </Badge>
                        ))}
                      </div>
                    </div>

                    <div className="grid grid-cols-3 gap-4 rounded-lg border bg-muted/30 p-3 text-center">
                      <div>
                        <div className="flex items-center justify-center gap-1 text-lg font-bold">
                          <Activity className="size-4" />
                          {agent.sessionsHandled}
                        </div>
                        <div className="text-xs text-muted-foreground">Sessions</div>
                      </div>
                      <div>
                        <div className="flex items-center justify-center gap-1 text-lg font-bold text-emerald-500">
                          <TrendingUp className="size-4" />
                          {agent.avgConfidence}%
                        </div>
                        <div className="text-xs text-muted-foreground">Confidence</div>
                      </div>
                      <div>
                        <div className="flex items-center justify-center gap-1 font-mono text-sm font-bold">
                          <Clock className="size-4" />
                          {agent.avgDuration}
                        </div>
                        <div className="text-xs text-muted-foreground">Avg Time</div>
                      </div>
                    </div>

                    <div className="flex items-center justify-between border-t pt-4">
                      <span className="text-sm">
                        {agent.isActive ? "Active" : "Disabled"}
                      </span>
                      <Switch
                        checked={agent.isActive}
                        onCheckedChange={() => toggleAgentStatus(agent.id)}
                      />
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          </TabsContent>
        </Tabs>
      </div>
    </div>
  )
}
