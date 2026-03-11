import { useParams } from "react-router-dom"
import { SectionCard } from "@/app/layout/AppLayout"

export function SessionDetailPage() {
  const { sessionId } = useParams()

  return (
    <div className="space-y-4">
      <h2 className="text-2xl font-bold">Session Detail</h2>
      <p className="text-sm text-muted-foreground">
        Session ID: <span className="font-mono">{sessionId}</span>
      </p>
      <div className="grid gap-4 lg:grid-cols-2">
        <SectionCard title="Timeline">
          待接入 <code>{`/api/sessions/${sessionId ?? ":id"}/timeline`}</code>
        </SectionCard>
        <SectionCard title="Diagnosis">
          待接入 <code>{`/api/sessions/${sessionId ?? ":id"}/diagnosis`}</code>
        </SectionCard>
        <SectionCard title="Tool Invocations">
          待接入 <code>{`/api/sessions/${sessionId ?? ":id"}/tool-invocations`}</code>
        </SectionCard>
        <SectionCard title="Todos">
          待接入 <code>{`/api/sessions/${sessionId ?? ":id"}/todos`}</code>
        </SectionCard>
      </div>
    </div>
  )
}
