using System.Collections.Concurrent;
using SreAgent.Application.Tools.Todo.Models;

namespace SreAgent.Application.Tools.Todo.Services;

/// <summary>
/// Service for managing todo items across sessions
/// </summary>
public class TodoService : ITodoService
{
    private readonly ConcurrentDictionary<Guid, List<TodoItem>> _sessionTodos = new();
    
    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> GetAsync(Guid sessionId)
    {
        var todos = _sessionTodos.TryGetValue(sessionId, out var list) 
            ? list.AsReadOnly() 
            : (IReadOnlyList<TodoItem>)Array.Empty<TodoItem>();
        
        return Task.FromResult(todos);
    }
    
    /// <inheritdoc />
    public Task UpdateAsync(Guid sessionId, IEnumerable<TodoItem> todos)
    {
        var todoList = todos.ToList();
        
        // Update timestamps for modified items
        foreach (var todo in todoList)
        {
            todo.UpdatedAt = DateTime.UtcNow;
            
            if (todo.Status == TodoStatus.Completed && todo.CompletedAt == null)
            {
                todo.CompletedAt = DateTime.UtcNow;
            }
        }
        
        _sessionTodos.AddOrUpdate(
            sessionId,
            _ => todoList,
            (_, _) => todoList);
        
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public Task ClearAsync(Guid sessionId)
    {
        _sessionTodos.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Interface for todo service
/// </summary>
public interface ITodoService
{
    /// <summary>
    /// Get all todos for a session
    /// </summary>
    Task<IReadOnlyList<TodoItem>> GetAsync(Guid sessionId);
    
    /// <summary>
    /// Update todos for a session (replaces all existing todos)
    /// </summary>
    Task UpdateAsync(Guid sessionId, IEnumerable<TodoItem> todos);
    
    /// <summary>
    /// Clear all todos for a session
    /// </summary>
    Task ClearAsync(Guid sessionId);
}
