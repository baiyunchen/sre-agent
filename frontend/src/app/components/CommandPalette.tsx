import { useEffect } from "react"
import { useNavigate } from "react-router-dom"
import {
  Activity,
  Settings,
  Shield,
  Clock,
  Wrench,
} from "lucide-react"
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from "@/components/ui/command"

interface CommandPaletteProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function CommandPalette({ open, onOpenChange }: CommandPaletteProps) {
  const navigate = useNavigate()

  useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === "k" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault()
        onOpenChange(!open)
      }
    }
    document.addEventListener("keydown", down)
    return () => document.removeEventListener("keydown", down)
  }, [open, onOpenChange])

  const handleSelect = (callback: () => void) => {
    onOpenChange(false)
    callback()
  }

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange}>
      <CommandInput placeholder="Search sessions, tools, agents..." />
      <CommandList>
        <CommandEmpty>No results found.</CommandEmpty>
        <CommandGroup heading="Quick Actions">
          <CommandItem onSelect={() => handleSelect(() => navigate("/"))}>
            <Activity className="mr-2 size-4" />
            <span>Dashboard Overview</span>
          </CommandItem>
          <CommandItem onSelect={() => handleSelect(() => navigate("/sessions"))}>
            <Clock className="mr-2 size-4" />
            <span>View All Sessions</span>
          </CommandItem>
          <CommandItem onSelect={() => handleSelect(() => navigate("/approvals"))}>
            <Shield className="mr-2 size-4" />
            <span>Check Pending Approvals</span>
          </CommandItem>
          <CommandItem onSelect={() => handleSelect(() => navigate("/tools"))}>
            <Wrench className="mr-2 size-4" />
            <span>Tools Registry</span>
          </CommandItem>
          <CommandItem onSelect={() => handleSelect(() => navigate("/settings"))}>
            <Settings className="mr-2 size-4" />
            <span>Open Settings</span>
          </CommandItem>
        </CommandGroup>
        <CommandSeparator />
      </CommandList>
    </CommandDialog>
  )
}
