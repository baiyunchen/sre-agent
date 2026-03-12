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
