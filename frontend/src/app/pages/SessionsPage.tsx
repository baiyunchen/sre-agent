import { useMemo, useState } from "react"
import { Link } from "react-router-dom"
import { useSessions } from "@/app/lib/hooks/useSessions"
import type { SessionSummary } from "@/app/lib/types"

function normalizeText(value: string | null): string {
  return (value ?? "").toLowerCase()
}

function matchSearch(item: SessionSummary, keyword: string): boolean {
  if (!keyword) {
    return true
  }
  const text = keyword.toLowerCase()
  return (
    normalizeText(item.alertName).includes(text) ||
    normalizeText(item.serviceName).includes(text) ||
    item.id.toLowerCase().includes(text)
  )
}

export function SessionsPage() {
  const [status, setStatus] = useState<string>("")
  const [source, setSource] = useState<string>("")
  const [search, setSearch] = useState("")

  const { data, isLoading, error } = useSessions({
    page: 1,
    pageSize: 20,
    status: status || undefined,
    source: source || undefined,
    search: search || undefined,
    sort: "createdAt",
    sortOrder: "desc",
  })

  const items = useMemo(
    () => (data?.items ?? []).filter((item) => matchSearch(item, search)),
    [data?.items, search],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-end justify-between gap-3">
        <div>
          <h2 className="text-2xl font-bold">Sessions</h2>
          <p className="text-sm text-muted-foreground">
            Figma Sessions 页签骨架，已对接 `GET /api/sessions`。
          </p>
        </div>
      </div>

      <div className="grid gap-3 rounded-lg border bg-card p-4 md:grid-cols-3">
        <label className="text-sm">
          <span className="mb-1 block text-muted-foreground">Status</span>
          <input
            className="w-full rounded-md border bg-background px-3 py-2"
            placeholder="Running / Completed ..."
            value={status}
            onChange={(event) => setStatus(event.target.value)}
          />
        </label>
        <label className="text-sm">
          <span className="mb-1 block text-muted-foreground">Source</span>
          <input
            className="w-full rounded-md border bg-background px-3 py-2"
            placeholder="CloudWatch / Prometheus ..."
            value={source}
            onChange={(event) => setSource(event.target.value)}
          />
        </label>
        <label className="text-sm">
          <span className="mb-1 block text-muted-foreground">Search</span>
          <input
            className="w-full rounded-md border bg-background px-3 py-2"
            placeholder="sessionId / alert / service"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </label>
      </div>

      <section className="overflow-hidden rounded-lg border bg-card">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 text-left">
            <tr>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Alert Name</th>
              <th className="px-4 py-3">Service</th>
              <th className="px-4 py-3">Source</th>
              <th className="px-4 py-3">Duration</th>
              <th className="px-4 py-3">Steps</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td className="px-4 py-4 text-muted-foreground" colSpan={6}>
                  Loading sessions...
                </td>
              </tr>
            )}
            {error instanceof Error && (
              <tr>
                <td className="px-4 py-4 text-destructive" colSpan={6}>
                  {error.message}
                </td>
              </tr>
            )}
            {!isLoading && !error && items.length === 0 && (
              <tr>
                <td className="px-4 py-4 text-muted-foreground" colSpan={6}>
                  No sessions found.
                </td>
              </tr>
            )}
            {items.map((session) => (
              <tr key={session.id} className="border-t">
                <td className="px-4 py-3">{session.status}</td>
                <td className="px-4 py-3">
                  <Link className="underline" to={`/sessions/${session.id}`}>
                    {session.alertName ?? session.id}
                  </Link>
                </td>
                <td className="px-4 py-3">{session.serviceName ?? "-"}</td>
                <td className="px-4 py-3">{session.source ?? "-"}</td>
                <td className="px-4 py-3">
                  {session.duration === null ? "-" : `${session.duration}s`}
                </td>
                <td className="px-4 py-3">{session.agentSteps ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  )
}
