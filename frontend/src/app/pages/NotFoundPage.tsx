import { Link } from "react-router-dom"

export function NotFoundPage() {
  return (
    <div className="space-y-2">
      <h2 className="text-2xl font-bold">Page Not Found</h2>
      <p className="text-sm text-muted-foreground">
        页面不存在，请返回 Dashboard。
      </p>
      <Link className="text-sm underline" to="/">
        返回 Dashboard
      </Link>
    </div>
  )
}
