import { useState, useEffect } from "react"
import { Wifi, WifiOff, RefreshCw } from "lucide-react"
import { Button } from "@/components/ui/button"
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import { useDashboardStream, type DashboardStreamStatus } from "@/app/lib/hooks/useDashboardStream"

interface ConnectionStatusProps {
  status: DashboardStreamStatus
  lastEventAt: string | null
}

export function ConnectionStatus({ status, lastEventAt }: ConnectionStatusProps) {
  const getStatusConfig = () => {
    switch (status) {
      case "connected":
        return {
          icon: Wifi,
          color: "text-emerald-500",
          bgColor: "bg-emerald-500",
          label: "Connected",
          description: `Real-time updates active${lastEventAt ? ` · Last: ${new Date(lastEventAt).toLocaleTimeString()}` : ""}`,
        }
      case "connecting":
        return {
          icon: RefreshCw,
          color: "text-amber-500",
          bgColor: "bg-amber-500 animate-pulse",
          label: "Connecting",
          description: "Connecting to server...",
        }
      case "disconnected":
        return {
          icon: WifiOff,
          color: "text-red-500",
          bgColor: "bg-red-500",
          label: "Disconnected",
          description: "Connection lost",
        }
    }
  }

  const config = getStatusConfig()
  const Icon = config.icon

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <div className="flex items-center gap-2">
          <div className={`size-2 rounded-full ${config.bgColor}`} />
          <span className={`text-sm ${config.color}`}>{config.label}</span>
        </div>
      </TooltipTrigger>
      <TooltipContent>
        <div className="flex items-center gap-2">
          <Icon className={`size-4 ${config.color}`} />
          <span>{config.description}</span>
        </div>
      </TooltipContent>
    </Tooltip>
  )
}

export function ConnectionStatusCompact({ status, lastEventAt }: ConnectionStatusProps) {
  const getStatusConfig = () => {
    switch (status) {
      case "connected":
        return { bgColor: "bg-emerald-500", label: "Connected" }
      case "connecting":
        return { bgColor: "bg-amber-500 animate-pulse", label: "Connecting" }
      case "disconnected":
        return { bgColor: "bg-red-500", label: "Disconnected" }
    }
  }

  const config = getStatusConfig()

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <div className="flex items-center gap-2 px-3 py-2">
          <div className={`size-2 rounded-full ${config.bgColor}`} />
          <span className="text-xs text-muted-foreground">{config.label}</span>
        </div>
      </TooltipTrigger>
      <TooltipContent side="right">
        <span>SSE connection: {config.label}</span>
      </TooltipContent>
    </Tooltip>
  )
}
