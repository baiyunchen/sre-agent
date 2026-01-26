using System.Text;
using SreAgent.Application.Tools.Todo.Models;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Prompts;
using SreAgent.Framework.Results;

namespace SreAgent.Application.Tools.Todo;

/// <summary>
/// Todo Read Tool - Read the current todo list for the session
/// </summary>
public class TodoReadTool : ToolBase<TodoReadParams>
{
    private static readonly Lazy<string> PromptContent = PromptLoader.CreateLazy<TodoReadTool>(
        "TodoReadPrompt.txt",
        "Use this tool to read your current todo list for the session.");
    
    private readonly ITodoService _todoService;
    
    public TodoReadTool(ITodoService todoService)
    {
        _todoService = todoService;
    }
    
    public override string Name => "todo_read";
    
    public override string Summary => "Read the current todo list for the session";
    
    public override string Description => PromptContent.Value;
    
    public override string Category => "Task Management";
    
    protected override string ReturnDescription => 
        "Returns the current todo list with all items and their status";
    
    protected override async Task<ToolResult> ExecuteAsync(
        TodoReadParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var todos = await _todoService.GetAsync(context.SessionId);
        
        if (todos.Count == 0)
        {
            return ToolResult.Success(
                "📋 No todos found for this session.",
                new { todos = Array.Empty<TodoItem>() });
        }
        
        var remainingCount = todos.Count(x => x.Status != TodoStatus.Completed && x.Status != TodoStatus.Cancelled);
        
        return ToolResult.Success(
            FormatTodoList(todos, remainingCount),
            new { todos });
    }
    
    private static string FormatTodoList(IReadOnlyList<TodoItem> todos, int remainingCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📋 Todo List ({remainingCount} remaining):");
        sb.AppendLine("─".PadRight(50, '─'));
        
        foreach (var todo in todos)
        {
            var statusIcon = todo.Status switch
            {
                TodoStatus.Pending => "⏳",
                TodoStatus.InProgress => "🔄",
                TodoStatus.Completed => "✅",
                TodoStatus.Cancelled => "❌",
                _ => "❓"
            };
            
            var priorityIcon = todo.Priority switch
            {
                TodoPriority.High => "🔴",
                TodoPriority.Medium => "🟡",
                TodoPriority.Low => "🟢",
                _ => "⚪"
            };
            
            sb.AppendLine($"{statusIcon} [{priorityIcon}] {todo.Id}: {todo.Content}");
        }
        
        return sb.ToString();
    }
}

#region Parameters

/// <summary>
/// Parameters for TodoReadTool (no parameters required)
/// </summary>
public class TodoReadParams
{
    // No parameters required for reading todos
}

#endregion
