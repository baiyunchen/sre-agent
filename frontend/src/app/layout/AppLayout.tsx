import { useState } from "react"
import { Link, Outlet, useLocation } from "react-router-dom"
import {
  LayoutDashboard,
  ListChecks,
  CheckSquare,
  Wrench,
  Bot,
  Settings,
  Search,
  ChevronLeft,
  ChevronRight,
  Shield,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb"
import { Separator } from "@/components/ui/separator"
import { cn } from "@/lib/utils"
import { CommandPalette } from "@/app/components/CommandPalette"
import { NotificationDropdown } from "@/app/components/NotificationDropdown"
import { ConnectionStatusCompact } from "@/app/components/ConnectionStatus"
import { useDashboardStream } from "@/app/lib/hooks/useDashboardStream"

const navigation = [
  { name: "Dashboard", href: "/", icon: LayoutDashboard },
  { name: "Sessions", href: "/sessions", icon: ListChecks },
  { name: "Approvals", href: "/approvals", icon: CheckSquare },
  { name: "Tools", href: "/tools", icon: Wrench },
  { name: "Agents", href: "/agents", icon: Bot },
  { name: "Settings", href: "/settings", icon: Settings },
]

export function AppLayout() {
  const [collapsed, setCollapsed] = useState(false)
  const [commandOpen, setCommandOpen] = useState(false)
  const location = useLocation()
  const stream = useDashboardStream(8, 12)

  const breadcrumbs = getBreadcrumbs(location.pathname)

  return (
    <div className="flex h-screen bg-background">
      <CommandPalette open={commandOpen} onOpenChange={setCommandOpen} />

      <aside
        className={cn(
          "flex flex-col border-r bg-sidebar transition-all duration-300",
          collapsed ? "w-16" : "w-64",
        )}
      >
        <div className="flex h-16 items-center gap-2 border-b px-4">
          <div className="flex size-8 items-center justify-center rounded-lg bg-emerald-500">
            <Shield className="size-5 text-white" />
          </div>
          {!collapsed && (
            <span className="font-semibold text-sidebar-foreground">SRE Agent</span>
          )}
        </div>

        <nav className="flex-1 p-2">
          <div className="flex flex-col gap-1">
            {navigation.map((item) => {
              const isActive =
                item.href === "/"
                  ? location.pathname === "/"
                  : location.pathname.startsWith(item.href)
              return (
                <Link key={item.name} to={item.href}>
                  <div
                    className={cn(
                      "relative flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors",
                      collapsed && "justify-center px-2",
                      isActive
                        ? "bg-sidebar-accent text-sidebar-foreground before:absolute before:left-0 before:top-0 before:h-full before:w-0.5 before:bg-sidebar-foreground"
                        : "text-sidebar-foreground/70 hover:bg-sidebar-accent/50 hover:text-sidebar-foreground",
                    )}
                  >
                    <item.icon className="size-4 shrink-0" />
                    {!collapsed && <span className="flex-1 text-left">{item.name}</span>}
                  </div>
                </Link>
              )
            })}
          </div>
        </nav>

        <div className="border-t p-4">
          {collapsed ? (
            <div className="flex justify-center">
              <div
                className={cn(
                  "size-2 rounded-full",
                  stream.status === "connected"
                    ? "bg-emerald-500"
                    : stream.status === "connecting"
                      ? "bg-amber-500 animate-pulse"
                      : "bg-red-500",
                )}
              />
            </div>
          ) : (
            <ConnectionStatusCompact status={stream.status} lastEventAt={stream.lastEventAt} />
          )}
        </div>

        <div className="border-t p-2">
          <Button
            variant="ghost"
            size="sm"
            className="w-full"
            onClick={() => setCollapsed(!collapsed)}
          >
            {collapsed ? (
              <ChevronRight className="size-4" />
            ) : (
              <>
                <ChevronLeft className="size-4" />
                <span className="ml-2">Collapse</span>
              </>
            )}
          </Button>
        </div>
      </aside>

      <div className="flex flex-1 flex-col overflow-hidden">
        <header className="flex h-16 items-center gap-4 border-b px-6">
          <Breadcrumb>
            <BreadcrumbList>
              {breadcrumbs.map((crumb, index) => (
                <div key={crumb.href} className="contents">
                  {index > 0 && <BreadcrumbSeparator />}
                  <BreadcrumbItem>
                    {index === breadcrumbs.length - 1 ? (
                      <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
                    ) : (
                      <BreadcrumbLink asChild>
                        <Link to={crumb.href}>{crumb.label}</Link>
                      </BreadcrumbLink>
                    )}
                  </BreadcrumbItem>
                </div>
              ))}
            </BreadcrumbList>
          </Breadcrumb>

          <div className="ml-auto flex items-center gap-4">
            <Button variant="outline" className="gap-2" onClick={() => setCommandOpen(true)}>
              <Search className="size-4" />
              <span className="hidden md:inline">Search</span>
              <kbd className="pointer-events-none ml-2 hidden h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100 md:inline-flex">
                <span className="text-xs">⌘</span>K
              </kbd>
            </Button>

            <NotificationDropdown />

            <Separator orientation="vertical" className="h-6" />

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" className="gap-2">
                  <Avatar className="size-8">
                    <AvatarFallback>SA</AvatarFallback>
                  </Avatar>
                  <span className="hidden md:inline">SRE Admin</span>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-56">
                <DropdownMenuLabel>
                  <div className="flex flex-col">
                    <span>SRE Admin</span>
                    <span className="text-xs font-normal text-muted-foreground">
                      admin@company.com
                    </span>
                  </div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem>Profile</DropdownMenuItem>
                <DropdownMenuItem>Preferences</DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem>Log out</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </header>

        <main className="flex-1 overflow-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

function getBreadcrumbs(pathname: string) {
  const segments = pathname.split("/").filter(Boolean)

  if (segments.length === 0) {
    return [{ label: "Dashboard", href: "/" }]
  }

  const breadcrumbs = [{ label: "Dashboard", href: "/" }]
  let currentPath = ""
  segments.forEach((segment) => {
    currentPath += `/${segment}`
    const label = segment.charAt(0).toUpperCase() + segment.slice(1)
    breadcrumbs.push({ label, href: currentPath })
  })

  return breadcrumbs
}
