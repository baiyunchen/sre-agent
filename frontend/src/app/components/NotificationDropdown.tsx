import { useState } from "react"
import { Link } from "react-router-dom"
import {
  Bell,
  AlertCircle,
  CheckCircle2,
  XCircle,
  Activity,
  Clock,
  Check,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { ScrollArea } from "@/components/ui/scroll-area"

type NotificationType = "alert" | "approval" | "completed" | "failed"

interface Notification {
  id: string
  type: NotificationType
  title: string
  message: string
  sessionId?: string
  timestamp: Date
  isRead: boolean
}

const notificationIcons: Record<NotificationType, typeof Activity> = {
  alert: Activity,
  approval: AlertCircle,
  completed: CheckCircle2,
  failed: XCircle,
}

const notificationColors: Record<NotificationType, string> = {
  alert: "text-blue-500",
  approval: "text-amber-500",
  completed: "text-emerald-500",
  failed: "text-red-500",
}

function formatTimestamp(date: Date): string {
  const now = new Date()
  const diff = Math.floor((now.getTime() - date.getTime()) / 1000)
  if (diff < 60) return `${diff}s ago`
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
  return date.toLocaleDateString()
}

export function NotificationDropdown() {
  const [notifications, setNotifications] = useState<Notification[]>([])
  const unreadCount = notifications.filter((n) => !n.isRead).length

  const markAllAsRead = () => {
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })))
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="relative">
          <Bell className="size-5" />
          {unreadCount > 0 && (
            <Badge
              variant="destructive"
              className="absolute -right-1 -top-1 size-5 rounded-full p-0 text-xs"
            >
              {unreadCount}
            </Badge>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-[380px]">
        <DropdownMenuLabel className="flex items-center justify-between">
          <span>Notifications</span>
          {unreadCount > 0 && (
            <Button
              variant="ghost"
              size="sm"
              onClick={markAllAsRead}
              className="h-auto p-0 text-xs text-muted-foreground hover:text-foreground"
            >
              <Check className="mr-1 size-3" />
              Mark all as read
            </Button>
          )}
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <ScrollArea className="h-[300px]">
          {notifications.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-8 text-center">
              <CheckCircle2 className="mb-2 size-12 text-muted-foreground" />
              <p className="text-sm font-medium">No notifications</p>
              <p className="text-xs text-muted-foreground">You&apos;re all caught up!</p>
            </div>
          ) : (
            notifications.map((notification) => {
              const Icon = notificationIcons[notification.type]
              const colorClass = notificationColors[notification.type]
              return (
                <DropdownMenuItem
                  key={notification.id}
                  className="flex cursor-pointer flex-col items-start gap-1 p-3"
                  asChild
                >
                  <Link to={notification.sessionId ? `/sessions/${notification.sessionId}` : "#"}>
                    <div className="flex w-full items-start gap-3">
                      <div className={`mt-0.5 ${colorClass}`}>
                        <Icon className="size-4" />
                      </div>
                      <div className="flex-1 gap-1">
                        <p className="text-sm font-medium">{notification.title}</p>
                        <p className="text-xs text-muted-foreground">{notification.message}</p>
                        <div className="flex items-center gap-1 text-xs text-muted-foreground">
                          <Clock className="size-3" />
                          {formatTimestamp(notification.timestamp)}
                        </div>
                      </div>
                      {!notification.isRead && (
                        <div className="mt-1 size-2 rounded-full bg-blue-500" />
                      )}
                    </div>
                  </Link>
                </DropdownMenuItem>
              )
            })
          )}
        </ScrollArea>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
