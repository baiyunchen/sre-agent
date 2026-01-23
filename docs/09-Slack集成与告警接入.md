# Slack 集成与告警接入

## 1. 概述

Slack 集成是 SRE Agent 与用户交互的主要渠道，需要实现：
- 接收告警消息
- 启动 Agent 处理
- 实时展示处理进度
- 支持人工审批
- 展示诊断结果

## 2. Slack App 配置

### 2.1 所需权限 (Scopes)

Bot Token Scopes:
- `chat:write` - 发送消息
- `chat:write.public` - 在公开频道发送消息
- `reactions:write` - 添加表情反应
- `files:write` - 上传文件
- `users:read` - 读取用户信息

Event Subscriptions:
- `message.channels` - 监听频道消息
- `app_mention` - 监听 @提及
- `reaction_added` - 监听表情反应

Interactivity:
- 启用交互式组件
- 配置 Request URL

### 2.2 环境配置

```yaml
slack:
  bot_token: "${SLACK_BOT_TOKEN}"
  signing_secret: "${SLACK_SIGNING_SECRET}"
  app_token: "${SLACK_APP_TOKEN}"  # Socket Mode
  alert_channel: "#alerts"
  notification_channel: "#sre-agent-updates"
```

## 3. 告警接入

### 3.1 告警消息解析

```csharp
public interface IAlertParser
{
    Alert? Parse(SlackMessage message);
    bool CanParse(SlackMessage message);
}

public class CloudWatchAlertParser : IAlertParser
{
    public bool CanParse(SlackMessage message)
    {
        return message.Text?.Contains("CloudWatch Alarm") == true
            || message.Attachments?.Any(a => a.Title?.Contains("ALARM") == true) == true;
    }
    
    public Alert? Parse(SlackMessage message)
    {
        // 解析 CloudWatch 告警格式
        var attachment = message.Attachments?.FirstOrDefault();
        if (attachment == null) return null;
        
        return new Alert
        {
            Id = Guid.NewGuid().ToString(),
            Name = ExtractAlarmName(attachment.Title),
            Service = ExtractService(attachment.Fields),
            Severity = DetermineSeverity(attachment.Color),
            Description = attachment.Text,
            Timestamp = message.Timestamp,
            Source = "CloudWatch",
            RawData = JsonSerializer.SerializeToDocument(message)
        };
    }
}

public class PrometheusAlertParser : IAlertParser
{
    public bool CanParse(SlackMessage message)
    {
        return message.Text?.Contains("[FIRING]") == true
            || message.Text?.Contains("[RESOLVED]") == true;
    }
    
    public Alert? Parse(SlackMessage message)
    {
        // 解析 Prometheus AlertManager 格式
        var match = Regex.Match(message.Text, @"\[FIRING:(\d+)\]\s+(.+)");
        if (!match.Success) return null;
        
        return new Alert
        {
            Id = Guid.NewGuid().ToString(),
            Name = match.Groups[2].Value,
            Service = ExtractLabel(message.Text, "service"),
            Severity = ExtractLabel(message.Text, "severity"),
            Description = message.Text,
            Timestamp = message.Timestamp,
            Source = "Prometheus",
            RawData = JsonSerializer.SerializeToDocument(message)
        };
    }
}

public class CompositeAlertParser : IAlertParser
{
    private readonly IEnumerable<IAlertParser> _parsers;
    
    public Alert? Parse(SlackMessage message)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(message))
            {
                return parser.Parse(message);
            }
        }
        return null;
    }
}
```

### 3.2 告警监听服务

```csharp
public class SlackAlertListener : BackgroundService
{
    private readonly ISlackClient _slackClient;
    private readonly IAlertParser _alertParser;
    private readonly ISessionService _sessionService;
    private readonly ILogger<SlackAlertListener> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _slackClient.ListenAsync(stoppingToken))
        {
            try
            {
                await ProcessMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Slack message");
            }
        }
    }
    
    private async Task ProcessMessageAsync(SlackMessage message)
    {
        // 跳过 bot 自己的消息
        if (message.BotId == _slackClient.BotId) return;
        
        // 解析告警
        var alert = _alertParser.Parse(message);
        if (alert == null) return;
        
        _logger.LogInformation("Received alert: {AlertName}", alert.Name);
        
        // 创建会话并开始处理
        var session = await _sessionService.CreateSessionAsync(alert);
        
        // 发送确认消息
        await SendAcknowledgementAsync(message.Channel, message.Timestamp, session);
        
        // 启动 Agent 处理
        _ = _sessionService.StartProcessingAsync(session.Id);
    }
    
    private async Task SendAcknowledgementAsync(
        string channel, string threadTs, Session session)
    {
        await _slackClient.PostMessageAsync(new SlackMessageRequest
        {
            Channel = channel,
            ThreadTs = threadTs,
            Text = $"🤖 SRE Agent 已收到告警，开始处理...",
            Blocks = new[]
            {
                new SectionBlock
                {
                    Text = new MarkdownText
                    {
                        Text = $"*告警*: {session.AlertName}\n*会话 ID*: `{session.Id}`"
                    }
                },
                new ActionsBlock
                {
                    Elements = new[]
                    {
                        new ButtonElement
                        {
                            Text = "查看详情",
                            Url = $"{_config.DashboardUrl}/sessions/{session.Id}"
                        }
                    }
                }
            }
        });
    }
}
```

## 4. 进度更新

### 4.1 进度消息模板

```csharp
public class SlackMessageBuilder
{
    public SlackMessageRequest BuildProgressMessage(Session session, string status)
    {
        var blocks = new List<IBlock>
        {
            new HeaderBlock
            {
                Text = $"🔍 正在处理: {session.AlertName}"
            },
            new SectionBlock
            {
                Fields = new[]
                {
                    new MarkdownText { Text = $"*状态*\n{status}" },
                    new MarkdownText { Text = $"*服务*\n{session.ServiceName}" }
                }
            },
            new DividerBlock()
        };
        
        // 添加 Agent 执行进度
        foreach (var run in session.AgentRuns.OrderBy(r => r.StartedAt))
        {
            var emoji = run.Status switch
            {
                AgentRunStatus.Running => "⏳",
                AgentRunStatus.Completed => "✅",
                AgentRunStatus.Failed => "❌",
                _ => "⬜"
            };
            
            blocks.Add(new ContextBlock
            {
                Elements = new[]
                {
                    new MarkdownText { Text = $"{emoji} *{run.AgentName}*: {GetAgentStatusText(run)}" }
                }
            });
        }
        
        return new SlackMessageRequest
        {
            Blocks = blocks
        };
    }
    
    public SlackMessageRequest BuildDiagnosisMessage(Session session)
    {
        var confidence = session.Confidence;
        var confidenceEmoji = confidence >= 0.8 ? "🟢" : confidence >= 0.5 ? "🟡" : "🔴";
        
        return new SlackMessageRequest
        {
            Blocks = new IBlock[]
            {
                new HeaderBlock { Text = "📋 诊断结果" },
                new SectionBlock
                {
                    Text = new MarkdownText
                    {
                        Text = session.DiagnosisSummary
                    }
                },
                new SectionBlock
                {
                    Fields = new[]
                    {
                        new MarkdownText { Text = $"*置信度*\n{confidenceEmoji} {confidence:P0}" },
                        new MarkdownText { Text = $"*处理耗时*\n{GetDuration(session)}" }
                    }
                },
                new DividerBlock(),
                new SectionBlock
                {
                    Text = new MarkdownText { Text = "*建议操作*" }
                },
                new SectionBlock
                {
                    Text = new MarkdownText
                    {
                        Text = FormatRecommendations(session.Diagnosis)
                    }
                },
                new ActionsBlock
                {
                    Elements = new IElement[]
                    {
                        new ButtonElement
                        {
                            Text = "查看详情",
                            Style = "primary",
                            Url = $"{_config.DashboardUrl}/sessions/{session.Id}"
                        },
                        new ButtonElement
                        {
                            Text = "标记已处理",
                            ActionId = "mark_resolved",
                            Value = session.Id.ToString()
                        },
                        new ButtonElement
                        {
                            Text = "需要帮助",
                            ActionId = "escalate",
                            Value = session.Id.ToString()
                        }
                    }
                }
            }
        };
    }
}
```

### 4.2 实时更新服务

```csharp
public class SlackProgressUpdater : 
    IEventHandler<AgentStartedEvent>,
    IEventHandler<AgentCompletedEvent>,
    IEventHandler<DiagnosisUpdatedEvent>,
    IEventHandler<SessionCompletedEvent>
{
    private readonly ISlackClient _slackClient;
    private readonly ISessionRepository _sessionRepository;
    private readonly SlackMessageBuilder _messageBuilder;
    
    // 消息 ID 映射
    private readonly ConcurrentDictionary<Guid, string> _messageMap = new();
    
    public async Task HandleAsync(AgentStartedEvent @event)
    {
        var session = await _sessionRepository.GetAsync(@event.SessionId);
        var message = _messageBuilder.BuildProgressMessage(session, "处理中...");
        
        if (_messageMap.TryGetValue(@event.SessionId, out var ts))
        {
            // 更新现有消息
            await _slackClient.UpdateMessageAsync(session.SlackChannel, ts, message);
        }
    }
    
    public async Task HandleAsync(SessionCompletedEvent @event)
    {
        var session = await _sessionRepository.GetAsync(@event.SessionId);
        var message = _messageBuilder.BuildDiagnosisMessage(session);
        
        // 发送最终诊断结果
        await _slackClient.PostMessageAsync(new SlackMessageRequest
        {
            Channel = session.SlackChannel,
            ThreadTs = session.SlackThreadTs,
            Blocks = message.Blocks
        });
    }
}
```

## 5. 人工审批交互

### 5.1 审批请求消息

```csharp
public class SlackApprovalService
{
    public async Task RequestApprovalAsync(ApprovalRequest request, Session session)
    {
        var message = new SlackMessageRequest
        {
            Channel = session.SlackChannel,
            ThreadTs = session.SlackThreadTs,
            Text = $"⚠️ 需要审批: {request.ToolName}",
            Blocks = new IBlock[]
            {
                new SectionBlock
                {
                    Text = new MarkdownText
                    {
                        Text = $"*工具*: `{request.ToolName}`\n*Agent*: {request.AgentId}"
                    }
                },
                new SectionBlock
                {
                    Text = new MarkdownText
                    {
                        Text = $"*参数*:\n```{FormatParameters(request.Parameters)}```"
                    }
                },
                new ActionsBlock
                {
                    BlockId = $"approval_{request.Id}",
                    Elements = new IElement[]
                    {
                        new ButtonElement
                        {
                            Text = "✅ 批准",
                            Style = "primary",
                            ActionId = "approve",
                            Value = request.Id.ToString()
                        },
                        new ButtonElement
                        {
                            Text = "✅ 永久批准",
                            ActionId = "approve_permanent",
                            Value = request.Id.ToString()
                        },
                        new ButtonElement
                        {
                            Text = "❌ 拒绝",
                            Style = "danger",
                            ActionId = "reject",
                            Value = request.Id.ToString()
                        },
                        new ButtonElement
                        {
                            Text = "❌ 永久拒绝",
                            ActionId = "reject_permanent",
                            Value = request.Id.ToString()
                        }
                    }
                }
            }
        };
        
        await _slackClient.PostMessageAsync(message);
    }
}
```

### 5.2 交互处理

```csharp
[ApiController]
[Route("api/slack")]
public class SlackInteractionController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly ISlackClient _slackClient;
    
    [HttpPost("interactions")]
    public async Task<IActionResult> HandleInteraction([FromForm] string payload)
    {
        var interaction = JsonSerializer.Deserialize<SlackInteraction>(payload);
        
        // 验证请求
        if (!VerifySlackSignature(Request))
        {
            return Unauthorized();
        }
        
        foreach (var action in interaction.Actions)
        {
            await HandleActionAsync(interaction, action);
        }
        
        return Ok();
    }
    
    private async Task HandleActionAsync(SlackInteraction interaction, SlackAction action)
    {
        var requestId = Guid.Parse(action.Value);
        var userId = interaction.User.Id;
        var userName = interaction.User.Name;
        
        ApprovalDecision decision = action.ActionId switch
        {
            "approve" => ApprovalDecision.AutoApprove,
            "approve_permanent" => ApprovalDecision.PermanentApprove,
            "reject" => ApprovalDecision.AutoDeny,
            "reject_permanent" => ApprovalDecision.PermanentDeny,
            _ => throw new InvalidOperationException($"Unknown action: {action.ActionId}")
        };
        
        await _approvalService.HandleApprovalDecisionAsync(requestId, decision, userName);
        
        // 更新消息
        var resultText = decision switch
        {
            ApprovalDecision.AutoApprove => $"✅ 已批准 (by @{userName})",
            ApprovalDecision.PermanentApprove => $"✅ 已永久批准 (by @{userName})",
            ApprovalDecision.AutoDeny => $"❌ 已拒绝 (by @{userName})",
            ApprovalDecision.PermanentDeny => $"❌ 已永久拒绝 (by @{userName})",
            _ => "处理完成"
        };
        
        await _slackClient.UpdateMessageAsync(
            interaction.Channel.Id,
            interaction.Message.Ts,
            new SlackMessageRequest { Text = resultText });
    }
}
```

## 6. 命令支持

### 6.1 Slash 命令

```csharp
[ApiController]
[Route("api/slack")]
public class SlackCommandController : ControllerBase
{
    [HttpPost("commands")]
    public async Task<IActionResult> HandleCommand([FromForm] SlackCommand command)
    {
        return command.Command switch
        {
            "/sre-status" => await HandleStatusCommand(command),
            "/sre-history" => await HandleHistoryCommand(command),
            "/sre-help" => await HandleHelpCommand(command),
            _ => BadRequest("Unknown command")
        };
    }
    
    private async Task<IActionResult> HandleStatusCommand(SlackCommand command)
    {
        // 获取当前活跃会话
        var activeSessions = await _sessionRepository
            .GetByStatusAsync(SessionStatus.Running);
        
        var response = new SlackCommandResponse
        {
            ResponseType = "in_channel",
            Blocks = BuildStatusBlocks(activeSessions)
        };
        
        return Ok(response);
    }
    
    private async Task<IActionResult> HandleHelpCommand(SlackCommand command)
    {
        var helpText = """
            *SRE Agent 命令*
            
            `/sre-status` - 查看当前活跃的处理会话
            `/sre-history [service]` - 查看历史处理记录
            `/sre-help` - 显示帮助信息
            
            *自动处理*
            当告警消息发送到配置的频道时，SRE Agent 会自动开始处理。
            
            *手动触发*
            @SRE Agent analyze <alert-description> - 手动触发分析
            """;
        
        return Ok(new SlackCommandResponse
        {
            ResponseType = "ephemeral",
            Text = helpText
        });
    }
}
```

### 6.2 App Mention 处理

```csharp
public class SlackMentionHandler
{
    public async Task HandleMentionAsync(SlackMessage message)
    {
        var text = message.Text.Replace($"<@{_botId}>", "").Trim();
        
        if (text.StartsWith("analyze", StringComparison.OrdinalIgnoreCase))
        {
            var description = text.Substring("analyze".Length).Trim();
            await HandleAnalyzeRequestAsync(message, description);
        }
        else if (text.StartsWith("status", StringComparison.OrdinalIgnoreCase))
        {
            await HandleStatusRequestAsync(message);
        }
        else
        {
            await SendHelpMessageAsync(message.Channel);
        }
    }
    
    private async Task HandleAnalyzeRequestAsync(SlackMessage message, string description)
    {
        // 创建手动触发的告警
        var alert = new Alert
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Manual Analysis Request",
            Description = description,
            Source = "Slack",
            Timestamp = DateTime.UtcNow
        };
        
        var session = await _sessionService.CreateSessionAsync(alert);
        session.SlackChannel = message.Channel;
        session.SlackThreadTs = message.Timestamp;
        
        await _sessionRepository.UpdateAsync(session);
        
        // 开始处理
        _ = _sessionService.StartProcessingAsync(session.Id);
        
        await _slackClient.PostMessageAsync(new SlackMessageRequest
        {
            Channel = message.Channel,
            ThreadTs = message.Timestamp,
            Text = $"🤖 开始分析，会话 ID: `{session.Id}`"
        });
    }
}
```

## 7. 配置示例

```yaml
slack:
  # OAuth
  bot_token: "${SLACK_BOT_TOKEN}"
  signing_secret: "${SLACK_SIGNING_SECRET}"
  
  # Channels
  alert_channels:
    - "#production-alerts"
    - "#staging-alerts"
  notification_channel: "#sre-agent-updates"
  
  # Alert Parsing
  parsers:
    - type: "cloudwatch"
      enabled: true
    - type: "prometheus"
      enabled: true
    - type: "custom"
      enabled: true
      pattern: "\\[ALERT\\].*"
  
  # Message Settings
  messages:
    progress_update_interval_seconds: 5
    max_message_length: 3000
    include_tool_details: true
    
  # Approval Settings
  approval:
    timeout_minutes: 5
    reminder_after_minutes: 2
    allowed_approvers:
      - "@sre-team"
      - "@oncall"
```
