using Microsoft.EntityFrameworkCore;
using Serilog;
using SreAgent.Application.Agents;
using SreAgent.Application.Services;
using SreAgent.Application.Tools.CloudWatch.Services;
using SreAgent.Application.Tools.KnowledgeBase.Services;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Contexts.Trimmers;
using SreAgent.Framework.Providers;
using SreAgent.Infrastructure.Persistence;
using SreAgent.Repository;

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
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendDev", policy =>
        {
            policy
                .WithOrigins("http://localhost:4173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    // 注册持久化服务
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Port=5432;Database=sre_agent;Username=sre_agent;Password=sre_agent";
    builder.Services.AddPersistence(connectionString);

    // 注册基础服务
    builder.Services.AddSingleton<ITodoService, TodoService>();
    builder.Services.AddSingleton(_ => ModelProvider.AliyunBailian());
    // IContextStore is registered via AddPersistence() above (PostgresContextStore)
    builder.Services.AddSingleton<ITokenEstimator, SimpleTokenEstimator>();

    // 注册 CloudWatch 服务
    // 使用默认凭证链（支持 AWS SSO、环境变量、配置文件等）
    builder.Services.AddSingleton<CloudWatchServiceOptions>(sp =>
    {
        var config = builder.Configuration.GetSection("AWS:CloudWatch");
        return new CloudWatchServiceOptions
        {
            Region = config["Region"] ?? Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-northeast-1",
            ServiceUrl = config["ServiceUrl"], // 可选，用于 LocalStack 等本地测试
            ProfileName = config["ProfileName"]
        };
    });
    builder.Services.AddSingleton<ICloudWatchService>(sp =>
        new CloudWatchService(
            sp.GetRequiredService<CloudWatchServiceOptions>(),
            sp.GetRequiredService<ILogger<CloudWatchService>>()));

    // 注册 Knowledge Base 服务（可选，需要配置 KnowledgeBaseId）
    var kbConfig = builder.Configuration.GetSection("AWS:KnowledgeBase");
    var kbOptions = new KnowledgeBaseServiceOptions
    {
        Region = kbConfig["Region"] ?? Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-northeast-1",
        ServiceUrl = kbConfig["ServiceUrl"],
        KnowledgeBaseId = kbConfig["KnowledgeBaseId"] ?? Environment.GetEnvironmentVariable("AWS_KNOWLEDGE_BASE_ID"),
        FoundationModelArn = kbConfig["FoundationModelArn"] ?? Environment.GetEnvironmentVariable("AWS_FOUNDATION_MODEL_ARN")
    };
    builder.Services.AddSingleton(kbOptions);
    
    // 只有配置了 KnowledgeBaseId 才注册服务
    if (!string.IsNullOrWhiteSpace(kbOptions.KnowledgeBaseId))
    {
        builder.Services.AddSingleton<IKnowledgeBaseService>(sp =>
            new KnowledgeBaseService(
                sp.GetRequiredService<KnowledgeBaseServiceOptions>(),
                sp.GetRequiredService<ILogger<KnowledgeBaseService>>()));
    }
    else
    {
        Log.Warning("Knowledge Base ID 未配置，Knowledge Base 功能将被禁用");
    }

    // 注册 ContextManagerOptions（包含剪枝器配置）
    builder.Services.AddSingleton(_ => new ContextManagerOptions
    {
        Trimmer = new RemoveOldestContextTrimmer(),
        TrimTargetRatio = 0.8
    });

    // 注册 Agent（scoped，因为诊断工具依赖 scoped 的 DbContext 和 Service）
    builder.Services.AddScoped<IAgent>(sp =>
        SreCoordinatorAgent.Create(
            sp.GetRequiredService<ModelProvider>(),
            sp.GetRequiredService<ITodoService>(),
            sp.GetRequiredService<ICloudWatchService>(),
            sp.GetService<IKnowledgeBaseService>(),
            sp.GetService<IDiagnosticDataService>(),
            sp.GetService<AppDbContext>(),
            sp.GetRequiredService<ILogger<ToolLoopAgent>>()));

    var app = builder.Build();

    // 自动应用数据库迁移
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            await dbContext.Database.MigrateAsync();
            Log.Information("Database migration applied successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database migration failed - database may not be available");
        }
    }

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
    app.UseCors("FrontendDev");

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
