import { createBrowserRouter } from "react-router-dom"
import { AppLayout } from "@/app/layout/AppLayout"
import { DashboardPage } from "@/app/pages/DashboardPage"
import { SessionsPage } from "@/app/pages/SessionsPage"
import { SessionDetailPage } from "@/app/pages/SessionDetailPage"
import { ApprovalsPage } from "@/app/pages/ApprovalsPage"
import { ToolsPage } from "@/app/pages/ToolsPage"
import { AgentsPage } from "@/app/pages/AgentsPage"
import { SettingsPage } from "@/app/pages/SettingsPage"
import { NotFoundPage } from "@/app/pages/NotFoundPage"

export const router = createBrowserRouter([
  {
    path: "/",
    element: <AppLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: "sessions", element: <SessionsPage /> },
      { path: "sessions/:sessionId", element: <SessionDetailPage /> },
      { path: "approvals", element: <ApprovalsPage /> },
      { path: "tools", element: <ToolsPage /> },
      { path: "agents", element: <AgentsPage /> },
      { path: "settings", element: <SettingsPage /> },
      { path: "*", element: <NotFoundPage /> },
    ],
  },
])
