export type SessionStatus =
  | "Created"
  | "Running"
  | "Completed"
  | "Failed"
  | "Interrupted"
  | "WaitingApproval"
  | "Cancelled"
  | "TimedOut"

export type SessionSource = "CloudWatch" | "Prometheus" | "Slack Manual" | string

export type SessionSeverity = "Critical" | "Warning" | "Info" | string

export interface SessionSummary {
  id: string
  status: SessionStatus | string
  alertName: string | null
  alertId: string | null
  serviceName: string | null
  source: SessionSource | null
  severity: SessionSeverity | null
  createdAt: string
  updatedAt: string
  duration: number | null
  agentSteps: number | null
}

export interface SessionListResponse {
  items: SessionSummary[]
  total: number
  page: number
  pageSize: number
}

export interface SessionsQuery {
  page?: number
  pageSize?: number
  status?: string
  source?: string
  sort?: "createdAt" | "updatedAt" | "status"
  sortOrder?: "asc" | "desc"
  search?: string
}

export interface SessionMessageRequest {
  message: string
  userId?: string
}

export interface SessionMessageTokenUsage {
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface SessionMessageResponse {
  sessionId: string
  output: string | null
  isSuccess: boolean
  error: string | null
  tokenUsage: SessionMessageTokenUsage
}

export interface TimelineEvent {
  id: string
  eventType: "message" | "agent_run" | "tool_invocation" | string
  timestamp: string
  title: string
  detail: string | null
  status: string | null
  actor: string | null
}

export interface SessionTimelineResponse {
  sessionId: string
  events: TimelineEvent[]
}

export interface SessionDiagnosisResponse {
  sessionId: string
  hypothesis: string
  confidence: number | null
  evidence: string[]
  recommendedActions: string[]
  totalRecords: number
  severityBreakdown: Record<string, number>
  sourceBreakdown: Record<string, number>
  timeWindowStart: string | null
  timeWindowEnd: string | null
}

export interface ToolInvocationSummary {
  id: string
  agentRunId: string
  toolName: string
  status: string
  approvalStatus: string | null
  errorMessage: string | null
  agentId: string | null
  agentName: string | null
  requestedAt: string
  completedAt: string | null
  durationMs: number
}

export interface SessionToolInvocationsResponse {
  sessionId: string
  items: ToolInvocationSummary[]
}

export interface SessionTodoItem {
  id: string
  content: string
  status: "pending" | "in_progress" | "completed" | "cancelled" | string
  priority: "low" | "medium" | "high" | string
  createdAt: string
  updatedAt: string
  completedAt: string | null
}

export interface SessionTodosResponse {
  sessionId: string
  items: SessionTodoItem[]
}
