using System.Collections.Concurrent;
using System.Reflection;

namespace SreAgent.Framework.Prompts;

/// <summary>
/// Utility class for loading prompt content from embedded resources or files.
/// Provides caching and fallback mechanisms for prompt loading.
/// </summary>
public static class PromptLoader
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();
    
    /// <summary>
    /// Load a prompt from an embedded resource or file system.
    /// Results are cached for subsequent calls.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resource</param>
    /// <param name="resourceName">The full resource name (e.g., "Namespace.Folder.FileName.txt")</param>
    /// <param name="fallbackContent">Content to return if the resource cannot be found</param>
    /// <returns>The prompt content</returns>
    public static string Load(Assembly assembly, string resourceName, string? fallbackContent = null)
    {
        var cacheKey = $"{assembly.FullName}:{resourceName}";
        
        return Cache.GetOrAdd(cacheKey, _ => LoadInternal(assembly, resourceName, fallbackContent));
    }
    
    /// <summary>
    /// Load a prompt using a type to determine the assembly and namespace.
    /// The resource name is constructed as: {Type.Namespace}.Prompts.{promptFileName}
    /// </summary>
    /// <typeparam name="T">A type in the same namespace as the prompts folder</typeparam>
    /// <param name="promptFileName">The prompt file name (e.g., "MyToolPrompt.txt")</param>
    /// <param name="fallbackContent">Content to return if the resource cannot be found</param>
    /// <returns>The prompt content</returns>
    public static string Load<T>(string promptFileName, string? fallbackContent = null)
    {
        var type = typeof(T);
        var assembly = type.Assembly;
        var resourceName = $"{type.Namespace}.Prompts.{promptFileName}";
        
        return Load(assembly, resourceName, fallbackContent);
    }
    
    /// <summary>
    /// Load a prompt from a specific namespace path.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resource</param>
    /// <param name="namespacePath">The namespace path (e.g., "SreAgent.Application.Tools.Todo")</param>
    /// <param name="promptFileName">The prompt file name (e.g., "TodoWritePrompt.txt")</param>
    /// <param name="fallbackContent">Content to return if the resource cannot be found</param>
    /// <returns>The prompt content</returns>
    public static string Load(Assembly assembly, string namespacePath, string promptFileName, string? fallbackContent = null)
    {
        var resourceName = $"{namespacePath}.Prompts.{promptFileName}";
        return Load(assembly, resourceName, fallbackContent);
    }
    
    /// <summary>
    /// Create a lazy-loaded prompt for deferred loading.
    /// Useful for static readonly fields in tool classes.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resource</param>
    /// <param name="resourceName">The full resource name</param>
    /// <param name="fallbackContent">Content to return if the resource cannot be found</param>
    /// <returns>A Lazy&lt;string&gt; that loads the prompt on first access</returns>
    public static Lazy<string> CreateLazy(Assembly assembly, string resourceName, string? fallbackContent = null)
    {
        return new Lazy<string>(() => Load(assembly, resourceName, fallbackContent));
    }
    
    /// <summary>
    /// Create a lazy-loaded prompt using a type to determine the assembly and namespace.
    /// </summary>
    /// <typeparam name="T">A type in the same namespace as the prompts folder</typeparam>
    /// <param name="promptFileName">The prompt file name</param>
    /// <param name="fallbackContent">Content to return if the resource cannot be found</param>
    /// <returns>A Lazy&lt;string&gt; that loads the prompt on first access</returns>
    public static Lazy<string> CreateLazy<T>(string promptFileName, string? fallbackContent = null)
    {
        return new Lazy<string>(() => Load<T>(promptFileName, fallbackContent));
    }
    
    /// <summary>
    /// Clear the prompt cache. Useful for testing or hot-reloading scenarios.
    /// </summary>
    public static void ClearCache()
    {
        Cache.Clear();
    }
    
    private static string LoadInternal(Assembly assembly, string resourceName, string? fallbackContent)
    {
        // Try loading from embedded resource first
        var content = TryLoadFromEmbeddedResource(assembly, resourceName);
        if (content != null)
        {
            return content;
        }
        
        // Try loading from file system (useful for development)
        content = TryLoadFromFileSystem(assembly, resourceName);
        if (content != null)
        {
            return content;
        }
        
        // Return fallback content or throw
        if (fallbackContent != null)
        {
            return fallbackContent;
        }
        
        throw new FileNotFoundException(
            $"Prompt resource not found: {resourceName}. " +
            $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
    }
    
    private static string? TryLoadFromEmbeddedResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return null;
        }
        
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    
    private static string? TryLoadFromFileSystem(Assembly assembly, string resourceName)
    {
        // Convert resource name to file path
        // e.g., "SreAgent.Application.Tools.Todo.Prompts.TodoWritePrompt.txt"
        // -> "Tools/Todo/Prompts/TodoWritePrompt.txt"
        
        var assemblyLocation = Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrEmpty(assemblyLocation))
        {
            return null;
        }
        
        // Try to find the file by converting dots to path separators
        var parts = resourceName.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }
        
        // Find where the actual path starts (after the namespace prefix)
        // We look for common folder names like "Tools", "Prompts", etc.
        var pathStartIndex = -1;
        var commonFolders = new[] { "Tools", "Prompts", "Resources" };
        
        for (var i = 0; i < parts.Length; i++)
        {
            if (commonFolders.Contains(parts[i]))
            {
                pathStartIndex = i;
                break;
            }
        }
        
        if (pathStartIndex == -1)
        {
            // Try using the last few parts as the path
            pathStartIndex = Math.Max(0, parts.Length - 3);
        }
        
        // Build the file path
        var pathParts = parts.Skip(pathStartIndex).ToArray();
        if (pathParts.Length == 0)
        {
            return null;
        }
        
        // The last part should be "filename.extension", but it's split as "filename" and "extension"
        var fileName = pathParts.Length >= 2 
            ? string.Join(".", pathParts[^2], pathParts[^1])
            : pathParts[^1];
        
        var directoryParts = pathParts.Take(pathParts.Length - 2).ToArray();
        var relativePath = Path.Combine(directoryParts.Append(fileName).ToArray());
        var fullPath = Path.Combine(assemblyLocation, relativePath);
        
        if (File.Exists(fullPath))
        {
            return File.ReadAllText(fullPath);
        }
        
        return null;
    }
}
