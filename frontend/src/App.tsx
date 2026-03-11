import { Button } from "@/components/ui/button"

function App() {
  return (
    <div className="flex min-h-svh items-center justify-center">
      <div className="flex flex-col items-center gap-6">
        <h1 className="text-4xl font-bold tracking-tight">SRE Agent</h1>
        <p className="text-muted-foreground">智能 SRE 运维助手</p>
        <Button>开始使用</Button>
      </div>
    </div>
  )
}

export default App
