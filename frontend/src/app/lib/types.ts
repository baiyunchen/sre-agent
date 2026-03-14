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

export interface TokenUsageInfo {
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface SessionMessageResponse {
  sessionId: string
  output: string | null
  isSuccess: boolean
  error: string | null
  tokenUsage: TokenUsageInfo
}

export interface SessionDetailResponse {
  id: string
  status: string
  alertId: string | null
  alertName: string | null
  source: string | null
  severity: string | null
  serviceName: string | null
  currentAgentId: string | null
  currentStep: number
  agentSteps: number
  diagnosisSummary: string | null
  confidence: number | null
  duration: number | null
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  updatedAt: string
  tokenUsage: TokenUsageInfo
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

export interface DashboardStatsResponse {
  totalSessionsToday: number
  autoResolutionRate: number
  avgProcessingTimeSeconds: number
  pendingApprovals: number
}

export interface DashboardActiveSessionSummary {
  id: string
  alertName: string | null
  serviceName: string | null
  status: string
  currentStep: number
  startedAt: string | null
  updatedAt: string
}

export interface DashboardActiveSessionsResponse {
  items: DashboardActiveSessionSummary[]
  total: number
}

export interface DashboardActivityItem {
  id: string
  sessionId: string
  eventType: string
  description: string | null
  actor: string | null
  occurredAt: string
}

export interface DashboardActivitiesResponse {
  items: DashboardActivityItem[]
  total: number
}

export interface DashboardSnapshotEvent {
  eventType: "dashboard.snapshot"
  generatedAt: string
  stats: DashboardStatsResponse
  activeSessions: DashboardActiveSessionsResponse
  activities: DashboardActivitiesResponse
}

export interface ApprovalPendingItem {
  sessionId: string
  alertName: string | null
  serviceName: string | null
  status: string
  updatedAt: string
}

export interface ApprovalPendingListResponse {
  items: ApprovalPendingItem[]
  total: number
}

export interface ApprovalDecisionRequest {
  approverId: string
  comment?: string
}

export interface ApprovalDecisionResponse {
  sessionId: string
  status: string
  message: string
}

export interface ApprovalHistoryItem {
  id: string
  sessionId: string
  action: "Approve" | "Reject"
  reason: string | null
  intervenedBy: string | null
  intervenedAt: string
}

export interface ApprovalHistoryResponse {
  items: ApprovalHistoryItem[]
  total: number
}

export interface ApprovalRuleItem {
  id: string
  toolName: string
  ruleType: "always-allow" | "always-deny"
  createdBy: string | null
  createdAt: string
}

export interface ApprovalRulesListResponse {
  items: ApprovalRuleItem[]
  total: number
}

export interface CreateApprovalRuleRequest {
  toolName: string
  ruleType: "always-allow" | "always-deny"
  createdBy?: string
}
