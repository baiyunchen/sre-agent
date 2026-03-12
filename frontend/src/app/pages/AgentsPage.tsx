import { useState } from "react"
import { Search, Bot, Activity, CheckCircle2, Settings } from "lucide-react"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Progress } from "@/components/ui/progress"

interface Agent {
  id: string
  name: string
  description: string
  type: "Coordinator" | "Specialist"
  status: "active" | "inactive"
  sessionsHandled: number
  successRate: number
  avgCompletionTime: number
  capabilities: string[]
}

const staticAgents: Agent[] = [
  {
    id: "agent_1",
    name: "SRE Coordinator",
    description:
      "Main orchestration agent that coordinates diagnosis and remediation workflows",
    type: "Coordinator",
    status: "active",
    sessionsHandled: 156,
    successRate: 94,
    avgCompletionTime: 180,
    capabilities: [
      "Orchestration",
      "Decision Making",
      "Tool Coordination",
      "Approval Management",
    ],
  },
  {
    id: "agent_2",
    name: "Log Analysis Agent",
    description:
      "Specialized in analyzing application and system logs to identify patterns and anomalies",
    type: "Specialist",
    status: "active",
    sessionsHandled: 89,
    successRate: 87,
    avgCompletionTime: 45,
    capabilities: [
      "Log Parsing",
      "Pattern Recognition",
      "Anomaly Detection",
      "CloudWatch Insights",
    ],
  },
  {
    id: "agent_3",
    name: "Database Diagnostics Agent",
    description:
      "Expert in diagnosing database performance issues, connection problems, and query optimization",
    type: "Specialist",
    status: "active",
    sessionsHandled: 67,
    successRate: 91,
    avgCompletionTime: 120,
    capabilities: [
      "Query Analysis",
      "Connection Pool Management",
      "Index Optimization",
      "Lock Detection",
    ],
  },
  {
    id: "agent_4",
    name: "Network Analysis Agent",
    description:
      "Analyzes network-related issues including latency, timeouts, and connectivity problems",
    type: "Specialist",
    status: "active",
    sessionsHandled: 42,
    successRate: 85,
    avgCompletionTime: 90,
    capabilities: [
      "Latency Analysis",
      "DNS Resolution",
      "Load Balancer Health",
      "Network Tracing",
    ],
  },
  {
    id: "agent_5",
    name: "Kubernetes Troubleshooter",
    description:
      "Specialized in diagnosing Kubernetes cluster and pod-level issues",
    type: "Specialist",
    status: "active",
    sessionsHandled: 78,
    successRate: 89,
    avgCompletionTime: 75,
    capabilities: [
      "Pod Inspection",
      "Resource Quotas",
      "Service Mesh",
      "Container Diagnostics",
    ],
  },
  {
    id: "agent_6",
    name: "Code Review Agent",
    description: "Analyzes recent code changes for potential issues causing alerts",
    type: "Specialist",
    status: "inactive",
    sessionsHandled: 12,
    successRate: 78,
    avgCompletionTime: 150,
    capabilities: ["Git Analysis", "Code Pattern Detection", "Deployment Correlation"],
  },
]

export function AgentsPage() {
  const [agents] = useState<Agent[]>(staticAgents)
  const [searchQuery, setSearchQuery] = useState("")

  const filteredAgents = agents.filter((agent) => {
    if (!searchQuery) return true
    const query = searchQuery.toLowerCase()
    return (
      agent.name.toLowerCase().includes(query) ||
      agent.description.toLowerCase().includes(query) ||
      agent.capabilities.some((c) => c.toLowerCase().includes(query))
    )
  })

  const activeAgents = agents.filter((a) => a.status === "active")
  const totalSessions = agents.reduce((sum, a) => sum + a.sessionsHandled, 0)
  const avgSuccessRate =
    agents.reduce((sum, a) => sum + a.successRate, 0) / agents.length

  return (
    <div className="container mx-auto max-w-screen-2xl p-6">
      <div className="flex flex-col gap-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">AI Agents</h1>
            <p className="text-muted-foreground">
              Configured AI agents and their performance metrics
            </p>
          </div>
          <Button className="gap-2">
            <Settings className="size-4" />
            Configure Agents
          </Button>
        </div>

        {/* Stats */}
        <div className="grid gap-4 md:grid-cols-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Total Agents</CardTitle>
              <Bot className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{agents.length}</div>
              <p className="text-xs text-muted-foreground">
                {activeAgents.length} active
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Sessions Handled</CardTitle>
              <Activity className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{totalSessions}</div>
              <p className="text-xs text-muted-foreground">Last 30 days</p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Avg Success Rate</CardTitle>
              <CheckCircle2 className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{avgSuccessRate.toFixed(0)}%</div>
              <Progress value={avgSuccessRate} className="mt-2 h-2" />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">Most Active</CardTitle>
              <Bot className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-sm font-medium">SRE Coordinator</div>
              <p className="text-xs text-muted-foreground">156 sessions</p>
            </CardContent>
          </Card>
        </div>

        {/* Search */}
        <Card>
          <CardContent className="pt-6">
            <div className="relative">
              <Search className="absolute left-3 top-3 size-4 text-muted-foreground" />
              <Input
                placeholder="Search agents by name, description, or capability..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pl-9"
              />
            </div>
          </CardContent>
        </Card>

        {/* Agents Grid */}
        <div className="grid gap-4 md:grid-cols-2">
          {filteredAgents.map((agent) => (
            <Card key={agent.id}>
              <CardHeader>
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex size-10 items-center justify-center rounded-lg bg-emerald-500/10">
                      <Bot className="size-5 text-emerald-500" />
                    </div>
                    <div>
                      <CardTitle>{agent.name}</CardTitle>
                      <div className="mt-1 flex items-center gap-2">
                        <Badge variant="secondary">{agent.type}</Badge>
                        <Badge
                          variant={agent.status === "active" ? "default" : "outline"}
                        >
                          {agent.status}
                        </Badge>
                      </div>
                    </div>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="flex flex-col gap-4">
                <p className="text-sm text-muted-foreground">{agent.description}</p>

                <div className="grid grid-cols-3 gap-4 rounded-lg bg-muted p-3">
                  <div className="text-center">
                    <div className="text-2xl font-bold">{agent.sessionsHandled}</div>
                    <div className="text-xs text-muted-foreground">Sessions</div>
                  </div>
                  <div className="text-center">
                    <div className="text-2xl font-bold text-emerald-500">
                      {agent.successRate}%
                    </div>
                    <div className="text-xs text-muted-foreground">Success</div>
                  </div>
                  <div className="text-center">
                    <div className="font-mono text-2xl font-bold">
                      {Math.floor(agent.avgCompletionTime / 60)}m
                    </div>
                    <div className="text-xs text-muted-foreground">Avg Time</div>
                  </div>
                </div>

                <div>
                  <h4 className="mb-2 text-sm font-semibold">Capabilities</h4>
                  <div className="flex flex-wrap gap-2">
                    {agent.capabilities.map((capability) => (
                      <Badge key={capability} variant="outline">
                        {capability}
                      </Badge>
                    ))}
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    </div>
  )
}
