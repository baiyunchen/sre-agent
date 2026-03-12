import { useState, useRef, useEffect } from "react"
import type { FormEvent } from "react"
import { useParams, Link } from "react-router-dom"
import {
  ArrowLeft,
  Copy,
  StopCircle,
  XCircle,
  ChevronDown,
  ChevronRight,
  Clock,
  CheckCircle2,
  AlertTriangle,
  Activity,
  Brain,
  Wrench,
  FileText,
  ThumbsUp,
  ThumbsDown,
  MinusCircle,
  Circle,
  CircleDot,
  ClipboardList,
  Send,
  User,
  Bot,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Separator } from "@/components/ui/separator"
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible"
import { Progress } from "@/components/ui/progress"
import { Input } from "@/components/ui/input"
import {
  useSessionDiagnosis,
  useSessionTimeline,
  useSessionTodos,
  useSessionToolInvocations,
} from "@/app/lib/hooks/useSessionDetailData"
import { useSessionMessage } from "@/app/lib/hooks/useSessionMessage"
import { MarkdownContent } from "@/app/components/MarkdownContent"
import type { TimelineEvent as TimelineEventType, SessionTodoItem } from "@/app/lib/types"

function formatDate(value: string | null | undefined): string {
  if (!value) return "-"
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleTimeString()
}

export function SessionDetailPage() {
  const { sessionId } = useParams()
  const [message, setMessage] = useState("")
  const [expandedEvents, setExpandedEvents] = useState<Set<string>>(new Set())
  const scrollRef = useRef<HTMLDivElement>(null)

  const timelineQuery = useSessionTimeline(sessionId)
  const diagnosisQuery = useSessionDiagnosis(sessionId)
  const toolInvocationsQuery = useSessionToolInvocations(sessionId)
  const todosQuery = useSessionTodos(sessionId)
  const sessionMessageMutation = useSessionMessage(sessionId)

  const events = timelineQuery.data?.events ?? []

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
    }
  }, [events.length])

  const canSend =
    Boolean(sessionId) && message.trim().length > 0 && !sessionMessageMutation.isPending

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!canSend) return
    await sessionMessageMutation.mutateAsync(message.trim())
    setMessage("")
  }

  const toggleEvent = (eventId: string) => {
    setExpandedEvents((prev) => {
      const next = new Set(prev)
      if (next.has(eventId)) next.delete(eventId)
      else next.add(eventId)
      return next
    })
  }

  const copySessionId = () => {
    if (sessionId) navigator.clipboard.writeText(sessionId)
  }

  return (
    <div className="h-[calc(100vh-4rem)] overflow-hidden">
      <div className="container mx-auto flex h-full max-w-screen-2xl flex-col">
        {/* Header */}
        <div className="shrink-0 p-6 pb-3">
          <div className="flex flex-col gap-4">
            <div className="flex items-start justify-between">
              <div className="flex flex-col gap-2">
                <div className="flex items-center gap-2">
                  <Button variant="ghost" size="icon" asChild>
                    <Link to="/sessions">
                      <ArrowLeft className="size-4" />
                    </Link>
                  </Button>
                  <h1 className="font-mono text-2xl font-bold">{sessionId}</h1>
                  <Button variant="ghost" size="icon" onClick={copySessionId}>
                    <Copy className="size-4" />
                  </Button>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Main Content */}
        <div className="min-h-0 flex-1 px-6 pb-6">
          <div className="grid h-full gap-6 lg:grid-cols-3">
            {/* Left Column - Timeline */}
            <div className="flex h-full flex-col lg:col-span-2">
              <Card className="flex h-full flex-col">
                <CardHeader className="shrink-0 pb-3">
                  <CardTitle>Agent Execution</CardTitle>
                  <CardDescription>Real-time agent activity and conversation</CardDescription>
                </CardHeader>
                <CardContent className="flex min-h-0 flex-1 flex-col">
                  <div ref={scrollRef} className="scrollbar-thin flex-1 overflow-y-auto pr-1">
                    {timelineQuery.isLoading && (
                      <p className="py-8 text-center text-sm text-muted-foreground">
                        Loading timeline...
                      </p>
                    )}
                    {timelineQuery.error instanceof Error && (
                      <p className="py-8 text-center text-sm text-destructive">
                        {timelineQuery.error.message}
                      </p>
                    )}
                    <div className="flex flex-col gap-1 pb-4">
                      {events.map((event) => (
                        <TimelineItem
                          key={event.id}
                          event={event}
                          isExpanded={expandedEvents.has(event.id)}
                          onToggle={() => toggleEvent(event.id)}
                        />
                      ))}
                    </div>
                  </div>

                  {/* Input Area */}
                  <div className="shrink-0 border-t pt-3">
                    <form
                      className="flex items-center gap-2"
                      onSubmit={handleSubmit}
                    >
                      <Input
                        value={message}
                        onChange={(e) => setMessage(e.target.value)}
                        placeholder="Type a message to the agent..."
                        className="flex-1"
                      />
                      <Button type="submit" size="icon" disabled={!canSend} className="shrink-0">
                        <Send className="size-4" />
                      </Button>
                    </form>
                  </div>
                </CardContent>
              </Card>
            </div>

            {/* Right Column - Tabs */}
            <div className="h-full">
              <Card className="flex h-full flex-col">
                <Tabs defaultValue="diagnosis" className="flex h-full flex-col">
                  <CardHeader className="shrink-0">
                    <TabsList className="grid w-full grid-cols-3">
                      <TabsTrigger value="diagnosis">Diagnosis</TabsTrigger>
                      <TabsTrigger value="tools">Tools</TabsTrigger>
                      <TabsTrigger value="todos">Todos</TabsTrigger>
                    </TabsList>
                  </CardHeader>
                  <CardContent className="scrollbar-thin min-h-0 flex-1 overflow-y-auto">
                    <TabsContent value="diagnosis" className="mt-0">
                      <DiagnosisPanel
                        isLoading={diagnosisQuery.isLoading}
                        error={diagnosisQuery.error}
                        data={diagnosisQuery.data}
                      />
                    </TabsContent>
                    <TabsContent value="tools" className="mt-0">
                      <ToolsPanel
                        isLoading={toolInvocationsQuery.isLoading}
                        error={toolInvocationsQuery.error}
                        items={toolInvocationsQuery.data?.items ?? []}
                      />
                    </TabsContent>
                    <TabsContent value="todos" className="mt-0">
                      <TodoListPanel
                        isLoading={todosQuery.isLoading}
                        error={todosQuery.error}
                        items={todosQuery.data?.items ?? []}
                      />
                    </TabsContent>
                  </CardContent>
                </Tabs>
              </Card>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

function TimelineItem({
  event,
  isExpanded,
  onToggle,
}: {
  event: TimelineEventType
  isExpanded: boolean
  onToggle: () => void
}) {
  const type = event.eventType

  if (type === "message" && event.actor?.toLowerCase() === "user") {
    return (
      <div className="flex justify-end py-1.5">
        <div className="flex max-w-[75%] items-start gap-2">
          <div className="rounded-2xl rounded-br-sm bg-blue-600 px-3.5 py-2 text-white">
            <MarkdownContent
              content={event.detail ?? event.title}
              className="text-sm leading-relaxed text-white [&_strong]:text-white [&_h3]:text-white [&_h4]:text-white [&_h5]:text-white [&_p]:mb-1 [&_p:last-child]:mb-0"
            />
            <p className="mt-1 text-[10px] text-blue-200">{formatDate(event.timestamp)}</p>
          </div>
          <div className="flex size-7 shrink-0 items-center justify-center rounded-full bg-blue-100">
            <User className="size-3.5 text-blue-600" />
          </div>
        </div>
      </div>
    )
  }

  if (type === "message" && event.actor?.toLowerCase() !== "user") {
    return (
      <div className="flex justify-start py-1.5">
        <div className="flex max-w-[75%] items-start gap-2">
          <div className="flex size-7 shrink-0 items-center justify-center rounded-full bg-gray-200">
            <Bot className="size-3.5 text-gray-700" />
          </div>
          <div className="rounded-2xl rounded-bl-sm bg-gray-100 px-3.5 py-2">
            <MarkdownContent
              content={event.detail ?? event.title}
              className="text-sm leading-relaxed [&_p]:mb-1 [&_p:last-child]:mb-0"
            />
            <p className="mt-1 text-[10px] text-muted-foreground">
              {formatDate(event.timestamp)}
            </p>
          </div>
        </div>
      </div>
    )
  }

  const iconMap: Record<string, typeof Activity> = {
    agent_run: Brain,
    tool_invocation: Wrench,
    diagnosis: FileText,
  }
  const Icon = iconMap[type] ?? Activity
  const hasDetail = Boolean(event.detail)

  return (
    <div className="py-0.5">
      <div className="rounded-lg border border-gray-200 bg-gray-50/50 px-2.5 py-1.5">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <Icon className="size-3.5 text-gray-500" />
            <span className="text-xs font-medium text-gray-700">{event.title}</span>
            {event.status === "running" && (
              <div className="size-3 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
            )}
          </div>
          <span className="font-mono text-[10px] text-muted-foreground">
            {formatDate(event.timestamp)}
          </span>
        </div>

        {hasDetail && (
          <Collapsible open={isExpanded} onOpenChange={onToggle}>
            <CollapsibleTrigger asChild>
              <button className="mt-1 flex items-center gap-1 text-xs text-muted-foreground transition-colors hover:text-foreground">
                {isExpanded ? <ChevronDown className="size-3" /> : <ChevronRight className="size-3" />}
                {isExpanded ? "Hide" : "Show"} details
              </button>
            </CollapsibleTrigger>
            <CollapsibleContent className="mt-2">
              <div className="scrollbar-thin max-h-80 overflow-y-auto rounded-md border bg-white/60 p-2.5">
                <MarkdownContent
                  content={event.detail ?? ""}
                  className="text-sm leading-relaxed text-muted-foreground"
                />
              </div>
            </CollapsibleContent>
          </Collapsible>
        )}
      </div>
    </div>
  )
}

function DiagnosisPanel({
  isLoading,
  error,
  data,
}: {
  isLoading: boolean
  error: Error | null
  data: any
}) {
  if (isLoading) return <p className="py-4 text-sm text-muted-foreground">Loading diagnosis...</p>
  if (error) return <p className="py-4 text-sm text-destructive">{error.message}</p>
  if (!data) return <p className="py-4 text-sm text-muted-foreground">No diagnosis data yet.</p>

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-2">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium">Confidence</span>
          <span className="text-2xl font-bold">
            {data.confidence != null ? `${data.confidence}%` : "N/A"}
          </span>
        </div>
        {data.confidence != null && (
          <Progress value={data.confidence} className="h-3" />
        )}
      </div>

      <Separator />

      <div className="flex flex-col gap-2">
        <h4 className="text-sm font-semibold">Hypothesis</h4>
        {data.hypothesis ? (
          <MarkdownContent
            content={data.hypothesis}
            className="text-sm text-muted-foreground"
          />
        ) : (
          <p className="text-sm text-muted-foreground">N/A</p>
        )}
      </div>

      <Separator />

      {(data.evidence ?? []).length > 0 && (
        <>
          <div className="flex flex-col gap-2">
            <h4 className="text-sm font-semibold">Evidence</h4>
            <ul className="flex flex-col gap-2">
              {(data.evidence as string[]).map((item: string, i: number) => (
                <li key={i} className="flex gap-2 text-sm">
                  <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-500" />
                  <MarkdownContent
                    content={item}
                    className="flex-1 text-muted-foreground [&_p]:mb-0"
                  />
                </li>
              ))}
            </ul>
          </div>
          <Separator />
        </>
      )}

      {(data.recommendedActions ?? []).length > 0 && (
        <div className="flex flex-col gap-2">
          <h4 className="text-sm font-semibold">Recommended Actions</h4>
          <ul className="flex flex-col gap-2">
            {(data.recommendedActions as string[]).map((item: string, i: number) => (
              <li key={i} className="flex gap-2 text-sm">
                <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-500" />
                <MarkdownContent
                  content={item}
                  className="flex-1 text-muted-foreground [&_p]:mb-0"
                />
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

function ToolsPanel({
  isLoading,
  error,
  items,
}: {
  isLoading: boolean
  error: Error | null
  items: any[]
}) {
  if (isLoading) return <p className="py-4 text-sm text-muted-foreground">Loading tools...</p>
  if (error) return <p className="py-4 text-sm text-destructive">{error.message}</p>
  if (items.length === 0)
    return <p className="py-4 text-sm text-muted-foreground">No tool invocations.</p>

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold">Tool Invocations</h3>
        <Badge variant="secondary" className="font-mono text-xs">
          {items.length} calls
        </Badge>
      </div>
      {items.map((tool: any) => (
        <div key={tool.id} className="flex flex-col gap-2 rounded-lg border p-3">
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-2">
              <Wrench className="size-4 text-muted-foreground" />
              <span className="font-medium">{tool.toolName}</span>
            </div>
            <Badge
              variant={
                tool.status === "Completed"
                  ? "default"
                  : tool.status === "Running"
                    ? "secondary"
                    : "destructive"
              }
            >
              {tool.status}
            </Badge>
          </div>
          {tool.durationMs > 0 && (
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <Clock className="size-3" />
              <span>{tool.durationMs}ms</span>
            </div>
          )}
          {tool.errorMessage && (
            <p className="text-xs text-destructive">{tool.errorMessage}</p>
          )}
        </div>
      ))}
    </div>
  )
}

function TodoListPanel({
  isLoading,
  error,
  items,
}: {
  isLoading: boolean
  error: Error | null
  items: SessionTodoItem[]
}) {
  if (isLoading) return <p className="py-4 text-sm text-muted-foreground">Loading todos...</p>
  if (error) return <p className="py-4 text-sm text-destructive">{error.message}</p>

  const completedCount = items.filter((t) => t.status === "completed").length

  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-center">
        <ClipboardList className="mb-3 size-12 text-muted-foreground/50" />
        <p className="text-sm text-muted-foreground">Agent hasn&apos;t created a todo list yet</p>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold">Agent Todo List</h3>
        <Badge variant="secondary" className="font-mono text-xs">
          {completedCount}/{items.length} completed
        </Badge>
      </div>
      <div className="flex flex-col gap-3">
        {items.map((todo) => (
          <TodoItem key={todo.id} todo={todo} />
        ))}
      </div>
    </div>
  )
}

function TodoItem({ todo }: { todo: SessionTodoItem }) {
  const getIcon = () => {
    switch (todo.status) {
      case "completed":
        return <CheckCircle2 className="size-4 shrink-0 text-emerald-500" />
      case "in_progress":
        return <CircleDot className="size-4 shrink-0 animate-pulse text-blue-500" />
      default:
        return <Circle className="size-4 shrink-0 text-gray-400" />
    }
  }

  const isInProgress = todo.status === "in_progress"
  const isCompleted = todo.status === "completed"

  return (
    <div
      className={`rounded-lg border p-3 ${
        isInProgress ? "border-l-4 border-l-blue-500 bg-blue-50/30" : ""
      }`}
    >
      <div className="flex items-start gap-3">
        <div className="mt-0.5">{getIcon()}</div>
        <div className="flex flex-1 flex-col gap-2">
          <p className={`text-sm ${isCompleted ? "text-muted-foreground line-through" : ""}`}>
            {todo.content}
          </p>
          <Badge
            variant="outline"
            className={
              isCompleted
                ? "w-fit border-emerald-500 text-emerald-700"
                : isInProgress
                  ? "w-fit animate-pulse border-blue-500 text-blue-700"
                  : "w-fit border-gray-300 text-gray-600"
            }
          >
            {todo.status === "completed"
              ? "Completed"
              : todo.status === "in_progress"
                ? "In Progress"
                : "Pending"}
          </Badge>
        </div>
      </div>
    </div>
  )
}
