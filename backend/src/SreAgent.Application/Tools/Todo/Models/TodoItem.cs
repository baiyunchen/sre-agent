using System.Text.Json.Serialization;

namespace SreAgent.Application.Tools.Todo.Models;

/// <summary>
/// Todo item representing a task in the analysis workflow
/// </summary>
public class TodoItem
{
    /// <summary>
    /// Unique identifier for the todo item
    /// </summary>
    public required string Id { get; set; }
    
    /// <summary>
    /// Brief description of the task
    /// </summary>
    public required string Content { get; set; }
    
    /// <summary>
    /// Current status of the task
    /// </summary>
    public TodoStatus Status { get; set; } = TodoStatus.Pending;
    
    /// <summary>
    /// Priority level of the task
    /// </summary>
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    
    /// <summary>
    /// When the task was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the task was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// When the task was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Task priority levels
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TodoPriority
{
    /// <summary>High priority</summary>
    High,
    /// <summary>Medium priority</summary>
    Medium,
    /// <summary>Low priority</summary>
    Low
}

/// <summary>
/// Task status values
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TodoStatus
{
    /// <summary>Task not yet started</summary>
    Pending,
    /// <summary>Currently working on</summary>
    InProgress,
    /// <summary>Task finished successfully</summary>
    Completed,
    /// <summary>Task no longer needed</summary>
    Cancelled
}
