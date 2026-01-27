using Serilog;
using SreAgent.Application.Agents;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Contexts.Trimmers;
using SreAgent.Framework.Providers;

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore.Routing", Serilog.Events.LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/sre-agent-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting SreAgent API...");

    var builder = WebApplication.CreateBuilder(args);

    // 使用 Serilog 替换默认日志
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // 注册基础服务
    builder.Services.AddSingleton<ITodoService, TodoService>();
    builder.Services.AddSingleton(_ => ModelProvider.AliyunBailian());
    builder.Services.AddSingleton<IContextStore, InMemoryContextStore>();
    builder.Services.AddSingleton<ITokenEstimator, SimpleTokenEstimator>();

    // 注册 ContextManagerOptions（包含剪枝器配置）
    builder.Services.AddSingleton(_ => new ContextManagerOptions
    {
        Trimmer = new RemoveOldestContextTrimmer(),
        TrimTargetRatio = 0.8
    });

    // 注册 Agent（单例，无状态可复用）
    builder.Services.AddSingleton<IAgent>(sp =>
        SreCoordinatorAgent.Create(
            sp.GetRequiredService<ModelProvider>(),
            sp.GetRequiredService<ITodoService>(),
            sp.GetRequiredService<ILogger<ToolLoopAgent>>()));

    var app = builder.Build();

    // 启用 Serilog HTTP 请求日志
    app.UseSerilogRequestLogging(options =>
    {
        // 自定义请求日志消息模板
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        
        // 附加诊断信息
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value!);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.MapControllers();

    Log.Information("SreAgent API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
