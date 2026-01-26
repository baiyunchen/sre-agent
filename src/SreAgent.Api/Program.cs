using SreAgent.Application.Tools;
using SreAgent.Framework.Providers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 注册 TodoTool 为单例（跨请求共享状态）
builder.Services.AddSingleton<TodoTool>();

// 注册 ModelProvider 为单例
builder.Services.AddSingleton(_ => ModelProvider.AliyunBailian());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
