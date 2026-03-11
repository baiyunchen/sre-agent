import { Link, Outlet, useLocation } from "react-router-dom"
import type { ReactNode } from "react"

type NavItem = { label: string; href: string }

const navItems: NavItem[] = [
  { label: "Dashboard", href: "/" },
  { label: "Sessions", href: "/sessions" },
  { label: "Approvals", href: "/approvals" },
  { label: "Tools", href: "/tools" },
  { label: "Agents", href: "/agents" },
  { label: "Settings", href: "/settings" },
]

export function AppLayout() {
  const location = useLocation()

  return (
    <div className="flex min-h-svh bg-background text-foreground">
      <aside className="w-64 border-r bg-sidebar">
        <div className="border-b px-4 py-4">
          <h1 className="text-lg font-semibold">SRE Agent</h1>
          <p className="text-xs text-muted-foreground">Figma Dashboard</p>
        </div>
        <nav className="p-2">
          {navItems.map((item) => {
            const active =
              item.href === "/"
                ? location.pathname === "/"
                : location.pathname.startsWith(item.href)
            return (
              <Link
                key={item.href}
                to={item.href}
                className={`mb-1 block rounded-md px-3 py-2 text-sm ${
                  active
                    ? "bg-sidebar-accent text-sidebar-accent-foreground"
                    : "text-sidebar-foreground/80 hover:bg-sidebar-accent/60"
                }`}
              >
                {item.label}
              </Link>
            )
          })}
        </nav>
      </aside>

      <main className="flex-1">
        <header className="border-b px-6 py-4">
          <h2 className="text-sm text-muted-foreground">SRE Agent Dashboard</h2>
        </header>
        <div className="p-6">
          <Outlet />
        </div>
      </main>
    </div>
  )
}

export function SectionCard({
  title,
  children,
}: {
  title: string
  children: ReactNode
}) {
  return (
    <section className="rounded-lg border bg-card p-4">
      <h3 className="mb-3 text-sm font-semibold">{title}</h3>
      {children}
    </section>
  )
}
