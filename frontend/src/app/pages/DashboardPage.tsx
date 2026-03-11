import { SectionCard } from "@/app/layout/AppLayout"

export function DashboardPage() {
  return (
    <div className="space-y-4">
      <h2 className="text-2xl font-bold">Dashboard</h2>
      <p className="text-sm text-muted-foreground">
        基于 Figma 设计稿的页面骨架，后续将对接 dashboard stats/activities/active-sessions API。
      </p>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <SectionCard title="Total Sessions Today">--</SectionCard>
        <SectionCard title="Auto-Resolution Rate">--</SectionCard>
        <SectionCard title="Avg Processing Time">--</SectionCard>
        <SectionCard title="Pending Approvals">--</SectionCard>
      </div>
    </div>
  )
}
