using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using SreAgent.Application.Tools.Todo.Models;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Prompts;
using SreAgent.Framework.Results;
using AllowedValuesAttribute = SreAgent.Framework.Abstractions.AllowedValuesAttribute;

namespace SreAgent.Application.Tools.Todo;

/// <summary>
/// Todo Write Tool - Create and manage a structured task list for analysis sessions
/// </summary>
public class TodoWriteTool : ToolBase<TodoWriteParams>
{
    private static readonly Lazy<string> PromptContent = PromptLoader.CreateLazy<TodoWriteTool>(
        "TodoWritePrompt.txt",
        "Use this tool to create and manage a structured task list for your current analysis session.");
    
    private readonly ITodoService _todoService;
    
    public TodoWriteTool(ITodoService todoService)
    {
        _todoService = todoService;
    }
    
    public override string Name => "todo_write";
    
    public override string Summary => "Create and manage a structured task list for analysis sessions";
    
    public override string Description => PromptContent.Value;
    
    public override string Category => "Task Management";
    
    protected override string ReturnDescription => 
        "Returns the updated todo list with count of remaining tasks";
    
    protected override async Task<ToolResult> ExecuteAsync(
        TodoWriteParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Convert parameters to TodoItem list
        var todoItems = parameters.Todos.Select(t => new TodoItem
        {
            Id = t.Id,
            Content = t.Content,
            Status = ParseStatus(t.Status),
            Priority = ParsePriority(t.Priority)
        }).ToList();
        
        await _todoService.UpdateAsync(context.SessionId, todoItems);
        
        var remainingCount = todoItems.Count(x => x.Status != TodoStatus.Completed && x.Status != TodoStatus.Cancelled);
        
        return ToolResult.Success(
            FormatTodoList(todoItems, remainingCount),
            new { todos = todoItems });
    }
    
    private static TodoStatus ParseStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" => TodoStatus.Pending,
            "in_progress" => TodoStatus.InProgress,
            "completed" => TodoStatus.Completed,
            "cancelled" => TodoStatus.Cancelled,
            _ => TodoStatus.Pending
        };
    }
    
    private static TodoPriority ParsePriority(string priority)
    {
        return priority.ToLowerInvariant() switch
        {
            "high" => TodoPriority.High,
            "medium" => TodoPriority.Medium,
            "low" => TodoPriority.Low,
            _ => TodoPriority.Medium
        };
    }
    
    private static string FormatTodoList(List<TodoItem> todos, int remainingCount)
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
/// Parameters for TodoWriteTool
/// </summary>
public class TodoWriteParams
{
    /// <summary>
    /// The updated todo list
    /// </summary>
    [Required(ErrorMessage = "Todos list is required")]
    [MinLength(1, ErrorMessage = "At least one todo item is required")]
    [Description("The updated todo list")]
    public List<TodoWriteItem> Todos { get; set; } = new();
}

/// <summary>
/// Individual todo item for write operation
/// </summary>
public class TodoWriteItem
{
    /// <summary>
    /// Unique identifier for the todo item
    /// </summary>
    [Required(ErrorMessage = "Todo id is required")]
    [Description("Unique identifier for the todo item")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Brief description of the task
    /// </summary>
    [Required(ErrorMessage = "Todo content is required")]
    [MaxLength(200, ErrorMessage = "Content must be 200 characters or less")]
    [Description("Brief description of the task")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of the task
    /// </summary>
    [Required(ErrorMessage = "Todo status is required")]
    [AllowedValues("pending", "in_progress", "completed", "cancelled")]
    [Description("Current status of the task: pending, in_progress, completed, cancelled")]
    public string Status { get; set; } = "pending";
    
    /// <summary>
    /// Priority level of the task
    /// </summary>
    [AllowedValues("high", "medium", "low")]
    [Description("Priority level of the task: high, medium, low")]
    public string Priority { get; set; } = "medium";
}

#endregion
