import { useState } from "react"
import {
  Database,
  Activity,
  Brain,
  Stethoscope,
  CloudCog,
  Search,
  ListChecks,
  Shield,
  XCircle,
  TrendingUp,
  Clock,
  BarChart3,
} from "lucide-react"
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

type ToolCategory = "Observability" | "Infrastructure" | "Knowledge" | "Diagnostic"

interface Tool {
  id: string
  name: string
  icon: typeof Activity
  category: ToolCategory
  description: string
  invocations: number
  successRate: number
  avgDuration: string
  requiresApproval: boolean
  isActive: boolean
}

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

const staticTools: Tool[] = [
  {
    id: "tool-001",
    name: "cloudwatch_query_logs",
    icon: CloudCog,
    category: "Observability",
    description: "Query CloudWatch logs for error patterns and anomalies",
    invocations: 342,
    successRate: 98.5,
    avgDuration: "2.3s",
    requiresApproval: false,
    isActive: true,
  },
  {
    id: "tool-002",
    name: "cloudwatch_get_metrics",
    icon: BarChart3,
    category: "Observability",
    description: "Retrieve CloudWatch metrics for specified time range",
    invocations: 287,
    successRate: 99.2,
    avgDuration: "1.8s",
    requiresApproval: false,
    isActive: true,
  },
  {
    id: "tool-003",
    name: "kubectl_get_logs",
    icon: Database,
    category: "Diagnostic",
    description: "Fetch Kubernetes pod logs for analysis",
    invocations: 156,
    successRate: 97.1,
    avgDuration: "3.1s",
    requiresApproval: false,
    isActive: true,
  },
  {
    id: "tool-004",
    name: "kubectl_delete_pod",
    icon: XCircle,
    category: "Infrastructure",
    description: "Delete a Kubernetes pod (requires approval)",
    invocations: 23,
    successRate: 100,
    avgDuration: "4.2s",
    requiresApproval: true,
    isActive: true,
  },
  {
    id: "tool-005",
    name: "query_knowledge_base",
    icon: Brain,
    category: "Knowledge",
    description: "Search internal knowledge base for runbooks and solutions",
    invocations: 412,
    successRate: 94.3,
    avgDuration: "1.2s",
    requiresApproval: false,
    isActive: true,
  },
  {
    id: "tool-006",
    name: "run_diagnostic_check",
    icon: Stethoscope,
    category: "Diagnostic",
    description: "Execute automated diagnostic checks on services",
    invocations: 198,
    successRate: 96.8,
    avgDuration: "5.7s",
    requiresApproval: false,
    isActive: true,
  },
  {
    id: "tool-007",
    name: "search_slack_history",
    icon: Search,
    category: "Knowledge",
    description: "Search Slack channel history for related incidents",
    invocations: 89,
    successRate: 91.0,
    avgDuration: "2.9s",
    requiresApproval: false,
    isActive: true,
  },
  {
    id: "tool-008",
    name: "create_todo_ticket",
    icon: ListChecks,
    category: "Infrastructure",
    description: "Create follow-up ticket in task management system",
    invocations: 67,
    successRate: 100,
    avgDuration: "1.5s",
    requiresApproval: false,
    isActive: true,
  },
]

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

const categoryColors: Record<ToolCategory, string> = {
  Observability: "border-blue-500 text-blue-500",
  Infrastructure: "border-red-500 text-red-500",
  Knowledge: "border-purple-500 text-purple-500",
  Diagnostic: "border-emerald-500 text-emerald-500",
}

export function ToolsPage() {
  const [tools, setTools] = useState<Tool[]>(staticTools)
  const [agents, setAgents] = useState<Agent[]>(staticAgents)
  const [searchQuery, setSearchQuery] = useState("")

  const toggleToolStatus = (id: string) => {
    setTools((prev) => prev.map((t) => (t.id === id ? { ...t, isActive: !t.isActive } : t)))
  }

  const toggleAgentStatus = (id: string) => {
    setAgents((prev) => prev.map((a) => (a.id === id ? { ...a, isActive: !a.isActive } : a)))
  }

  const filteredTools = tools.filter(
    (tool) =>
      tool.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      tool.description.toLowerCase().includes(searchQuery.toLowerCase()),
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
          <p className="text-muted-foreground">Manage registered tools and AI agents</p>
        </div>

        <div className="flex items-center gap-4">
          <Input
            placeholder="Search tools and agents..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="max-w-sm"
          />
        </div>

        <Tabs defaultValue="tools" className="flex flex-col gap-4">
          <TabsList>
            <TabsTrigger value="tools">Tools ({tools.length})</TabsTrigger>
            <TabsTrigger value="agents">Agents ({agents.length})</TabsTrigger>
          </TabsList>

          <TabsContent value="tools" className="flex flex-col gap-4">
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
              {filteredTools.map((tool) => {
                const Icon = tool.icon
                return (
                  <Card key={tool.id} className={!tool.isActive ? "opacity-60" : ""}>
                    <CardHeader>
                      <div className="flex items-start justify-between">
                        <div className="flex items-start gap-3">
                          <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-muted">
                            <Icon className="size-5" />
                          </div>
                          <div className="min-w-0 flex-1">
                            <CardTitle className="text-base">{tool.name}</CardTitle>
                            <div className="mt-1 flex flex-wrap items-center gap-2">
                              <Badge
                                variant="outline"
                                className={categoryColors[tool.category]}
                              >
                                {tool.category}
                              </Badge>
                              {tool.requiresApproval && (
                                <Badge
                                  variant="outline"
                                  className="border-amber-500 text-amber-500"
                                >
                                  <Shield className="size-3" />
                                  Requires Approval
                                </Badge>
                              )}
                            </div>
                          </div>
                        </div>
                      </div>
                    </CardHeader>
                    <CardContent className="flex flex-col gap-4">
                      <p className="text-sm text-muted-foreground">{tool.description}</p>

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
                          <div className="font-mono text-sm font-bold">{tool.avgDuration}</div>
                          <div className="text-xs text-muted-foreground">Avg Time</div>
                        </div>
                      </div>

                      <div className="flex items-center justify-between border-t pt-4">
                        <div className="flex items-center gap-2">
                          <div
                            className={`size-2 rounded-full ${tool.isActive ? "bg-emerald-500" : "bg-gray-500"}`}
                          />
                          <span className="text-sm">
                            {tool.isActive ? "Active" : "Disabled"}
                          </span>
                        </div>
                        <Switch
                          checked={tool.isActive}
                          onCheckedChange={() => toggleToolStatus(tool.id)}
                        />
                      </div>
                    </CardContent>
                  </Card>
                )
              })}
            </div>
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
