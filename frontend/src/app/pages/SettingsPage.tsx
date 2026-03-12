import { useState } from "react"
import {
  Globe,
  Brain,
  MessageSquare,
  FileSearch,
  Shield,
  Activity,
  Database,
  Save,
  CheckCircle2,
  Eye,
  EyeOff,
} from "lucide-react"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Textarea } from "@/components/ui/textarea"
import { Separator } from "@/components/ui/separator"
import { toast } from "sonner"

export function SettingsPage() {
  const [showApiKey, setShowApiKey] = useState(false)
  const [showSlackToken, setShowSlackToken] = useState(false)

  const handleSave = () => {
    toast.success("Settings saved successfully!", {
      description: "Your configuration has been updated.",
    })
  }

  return (
    <div className="container mx-auto max-w-screen-2xl p-6">
      <div className="flex flex-col gap-6">
        <div>
          <h1 className="text-3xl font-bold">Settings</h1>
          <p className="text-muted-foreground">Configure your SRE Agent system</p>
        </div>

        <Tabs defaultValue="general" orientation="vertical" className="gap-6">
          <TabsList variant="line" className="w-48 shrink-0 gap-1">
            <TabsTrigger value="general" className="justify-start gap-2 px-3 py-2">
              <Globe className="size-4" />
              General
            </TabsTrigger>
            <TabsTrigger value="llm" className="justify-start gap-2 px-3 py-2">
              <Brain className="size-4" />
              LLM Configuration
            </TabsTrigger>
            <TabsTrigger value="slack" className="justify-start gap-2 px-3 py-2">
              <MessageSquare className="size-4" />
              Slack Integration
            </TabsTrigger>
            <TabsTrigger value="parsers" className="justify-start gap-2 px-3 py-2">
              <FileSearch className="size-4" />
              Alert Parsers
            </TabsTrigger>
            <TabsTrigger value="approvals" className="justify-start gap-2 px-3 py-2">
              <Shield className="size-4" />
              Approval Settings
            </TabsTrigger>
            <TabsTrigger value="observability" className="justify-start gap-2 px-3 py-2">
              <Activity className="size-4" />
              Observability
            </TabsTrigger>
            <TabsTrigger value="database" className="justify-start gap-2 px-3 py-2">
              <Database className="size-4" />
              Database
            </TabsTrigger>
          </TabsList>

          <div className="flex-1">
              <TabsContent value="general" className="m-0 flex flex-col gap-6">
                <Card>
                  <CardHeader>
                    <CardTitle>General Settings</CardTitle>
                    <CardDescription>Configure basic system settings</CardDescription>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-4">
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="system-name">System Name</Label>
                      <Input id="system-name" defaultValue="SRE Agent Production" />
                    </div>
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="dashboard-url">Dashboard URL</Label>
                      <Input
                        id="dashboard-url"
                        defaultValue="https://sre-agent.company.com"
                      />
                    </div>
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="timezone">Timezone</Label>
                      <Select defaultValue="asia-shanghai">
                        <SelectTrigger id="timezone">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="utc">UTC</SelectItem>
                          <SelectItem value="est">Eastern Time</SelectItem>
                          <SelectItem value="pst">Pacific Time</SelectItem>
                          <SelectItem value="asia-shanghai">Asia/Shanghai</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="llm" className="m-0 flex flex-col gap-6">
                <Card>
                  <CardHeader>
                    <CardTitle>LLM Configuration</CardTitle>
                    <CardDescription>
                      Configure AI model providers and parameters
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-4">
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="model-provider">Model Provider</Label>
                      <Select defaultValue="aliyun">
                        <SelectTrigger id="model-provider">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="aliyun">
                            Aliyun Bailian (通义千问)
                          </SelectItem>
                          <SelectItem value="zhipu">Zhipu AI (智谱清言)</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="api-key">API Key</Label>
                      <div className="flex gap-2">
                        <Input
                          id="api-key"
                          type={showApiKey ? "text" : "password"}
                          defaultValue="sk-abc123...xyz789"
                          className="font-mono"
                        />
                        <Button
                          variant="outline"
                          size="icon"
                          onClick={() => setShowApiKey(!showApiKey)}
                        >
                          {showApiKey ? (
                            <EyeOff className="size-4" />
                          ) : (
                            <Eye className="size-4" />
                          )}
                        </Button>
                      </div>
                    </div>
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="model-name">Model Name</Label>
                      <Select defaultValue="qwen-plus">
                        <SelectTrigger id="model-name">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="qwen-plus">qwen-plus</SelectItem>
                          <SelectItem value="qwen-turbo">qwen-turbo</SelectItem>
                          <SelectItem value="qwen-max">qwen-max</SelectItem>
                          <SelectItem value="glm-4">glm-4</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="flex flex-col gap-2">
                        <Label htmlFor="temperature">Temperature</Label>
                        <Input
                          id="temperature"
                          type="number"
                          defaultValue="0.7"
                          step="0.1"
                          min="0"
                          max="2"
                        />
                      </div>
                      <div className="flex flex-col gap-2">
                        <Label htmlFor="max-tokens">Max Tokens</Label>
                        <Input id="max-tokens" type="number" defaultValue="4096" />
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="slack" className="m-0 flex flex-col gap-6">
                <Card>
                  <CardHeader>
                    <CardTitle>Slack Integration</CardTitle>
                    <CardDescription>
                      Configure Slack bot and notification settings
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-4">
                    <div className="flex items-center justify-between">
                      <div>
                        <Label>Enable Slack Integration</Label>
                        <p className="text-sm text-muted-foreground">
                          Connect to Slack for alerts and notifications
                        </p>
                      </div>
                      <Switch defaultChecked />
                    </div>
                    <Separator />
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="bot-token">Bot Token</Label>
                      <div className="flex gap-2">
                        <Input
                          id="bot-token"
                          type={showSlackToken ? "text" : "password"}
                          defaultValue="xoxb-..."
                          className="font-mono"
                        />
                        <Button
                          variant="outline"
                          size="icon"
                          onClick={() => setShowSlackToken(!showSlackToken)}
                        >
                          {showSlackToken ? (
                            <EyeOff className="size-4" />
                          ) : (
                            <Eye className="size-4" />
                          )}
                        </Button>
                      </div>
                    </div>
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="alert-channels">Alert Channels</Label>
                      <Textarea
                        id="alert-channels"
                        placeholder="#sre-alerts, #production-incidents"
                        defaultValue="#sre-alerts, #production-incidents"
                        rows={2}
                      />
                      <p className="text-xs text-muted-foreground">
                        Comma-separated list of channels to monitor
                      </p>
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="parsers" className="m-0 flex flex-col gap-6">
                <Card>
                  <CardHeader>
                    <CardTitle>Alert Parsers</CardTitle>
                    <CardDescription>
                      Configure alert parsing for different sources
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-6">
                    <div className="flex items-center justify-between">
                      <div>
                        <Label>CloudWatch Parser</Label>
                        <p className="text-sm text-muted-foreground">
                          Parse CloudWatch alarm notifications
                        </p>
                      </div>
                      <Switch defaultChecked />
                    </div>
                    <Separator />
                    <div className="flex items-center justify-between">
                      <div>
                        <Label>Prometheus Parser</Label>
                        <p className="text-sm text-muted-foreground">
                          Parse Prometheus AlertManager webhooks
                        </p>
                      </div>
                      <Switch defaultChecked />
                    </div>
                    <Separator />
                    <div className="flex flex-col gap-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <Label>Custom Parser</Label>
                          <p className="text-sm text-muted-foreground">
                            Parse alerts using custom regex patterns
                          </p>
                        </div>
                        <Switch />
                      </div>
                      <div className="flex flex-col gap-2">
                        <Label htmlFor="custom-regex">Regex Pattern</Label>
                        <Textarea
                          id="custom-regex"
                          placeholder='Alert: (?P<alert_name>.*) - Service: (?P<service>.*)'
                          rows={3}
                          className="font-mono text-xs"
                        />
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="approvals" className="m-0 flex flex-col gap-6">
                <Card>
                  <CardHeader>
                    <CardTitle>Approval Settings</CardTitle>
                    <CardDescription>
                      Configure tool execution approval workflow
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-4">
                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="flex flex-col gap-2">
                        <Label htmlFor="approval-timeout">
                          Default Timeout (minutes)
                        </Label>
                        <Input
                          id="approval-timeout"
                          type="number"
                          defaultValue="15"
                        />
                      </div>
                      <div className="flex flex-col gap-2">
                        <Label htmlFor="reminder-interval">
                          Reminder Interval (minutes)
                        </Label>
                        <Input
                          id="reminder-interval"
                          type="number"
                          defaultValue="5"
                        />
                      </div>
                    </div>
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="approver-groups">Allowed Approver Groups</Label>
                      <Textarea
                        id="approver-groups"
                        defaultValue="sre-team, platform-team, on-call-engineers"
                        rows={2}
                      />
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="observability" className="m-0 flex flex-col gap-6">
                <Card>
                  <CardHeader>
                    <CardTitle>Observability</CardTitle>
                    <CardDescription>
                      Configure metrics, tracing, and logging
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-6">
                    <div className="flex flex-col gap-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <Label>Metrics Export</Label>
                          <p className="text-sm text-muted-foreground">
                            Export metrics in Prometheus format
                          </p>
                        </div>
                        <Switch defaultChecked />
                      </div>
                      <div className="flex flex-col gap-2">
                        <Label htmlFor="metrics-endpoint">Prometheus Endpoint</Label>
                        <Input
                          id="metrics-endpoint"
                          defaultValue="/metrics"
                          className="font-mono"
                        />
                      </div>
                    </div>
                    <Separator />
                    <div className="flex flex-col gap-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <Label>Distributed Tracing</Label>
                          <p className="text-sm text-muted-foreground">
                            Enable OpenTelemetry tracing
                          </p>
                        </div>
                        <Switch defaultChecked />
                      </div>
                      <div className="flex flex-col gap-2">
                        <Label htmlFor="otlp-endpoint">OTLP Endpoint</Label>
                        <Input
                          id="otlp-endpoint"
                          defaultValue="http://localhost:4318"
                          className="font-mono"
                        />
                      </div>
                    </div>
                    <Separator />
                    <div className="flex flex-col gap-2">
                      <Label htmlFor="log-level">Log Level</Label>
                      <Select defaultValue="info">
                        <SelectTrigger id="log-level">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="debug">Debug</SelectItem>
                          <SelectItem value="info">Info</SelectItem>
                          <SelectItem value="warn">Warning</SelectItem>
                          <SelectItem value="error">Error</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <TabsContent value="database" className="m-0 flex flex-col gap-6">
                <Card>
                  <CardHeader>
                    <CardTitle>Database</CardTitle>
                    <CardDescription>
                      Monitor database connection and migrations
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="flex flex-col gap-4">
                    <div className="flex items-center justify-between rounded-lg border p-4">
                      <div className="flex items-center gap-3">
                        <CheckCircle2 className="size-5 text-emerald-500" />
                        <div>
                          <Label>Connection Status</Label>
                          <p className="text-sm text-muted-foreground">
                            PostgreSQL 14.5
                          </p>
                        </div>
                      </div>
                      <Badge className="bg-emerald-500">Connected</Badge>
                    </div>
                    <div className="flex items-center justify-between rounded-lg border p-4">
                      <div className="flex items-center gap-3">
                        <CheckCircle2 className="size-5 text-emerald-500" />
                        <div>
                          <Label>Migration Status</Label>
                          <p className="text-sm text-muted-foreground">
                            All migrations applied
                          </p>
                        </div>
                      </div>
                      <Badge variant="outline">Up to date</Badge>
                    </div>
                    <Separator />
                    <div className="flex flex-col gap-2">
                      <Label>Database URL</Label>
                      <Input
                        type="password"
                        defaultValue="postgresql://user:pass@localhost:5432/sreagent"
                        className="font-mono text-xs"
                        disabled
                      />
                      <p className="text-xs text-muted-foreground">
                        Environment variable: DATABASE_URL
                      </p>
                    </div>
                  </CardContent>
                </Card>
              </TabsContent>

              <div className="flex justify-end gap-2">
                <Button variant="outline">Reset to Defaults</Button>
                <Button onClick={handleSave}>
                  <Save className="size-4" />
                  Save Changes
                </Button>
              </div>
            </div>
        </Tabs>
      </div>
    </div>
  )
}
