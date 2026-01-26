using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Abstractions;

/// <summary>
/// 强类型工具基类
/// 子类只需要关注业务逻辑，不需要处理 JSON 序列化
/// </summary>
/// <typeparam name="TParams">参数类型</typeparam>
public abstract class ToolBase<TParams> : ITool where TParams : class, new()
{
    private readonly Lazy<string> _parameterSchema;
    private readonly Lazy<ToolDetail> _toolDetail;
    
    protected ToolBase()
    {
        _parameterSchema = new Lazy<string>(GenerateParameterSchema);
        _toolDetail = new Lazy<ToolDetail>(BuildToolDetail);
    }
    
    /// <inheritdoc />
    public abstract string Name { get; }
    
    /// <inheritdoc />
    public abstract string Summary { get; }
    
    /// <inheritdoc />
    public abstract string Description { get; }
    
    /// <inheritdoc />
    public virtual string Category => "General";
    
    /// <summary>
    /// 返回值说明（可选）
    /// </summary>
    protected virtual string? ReturnDescription => null;
    
    /// <summary>
    /// 使用示例（可选）
    /// </summary>
    protected virtual IReadOnlyList<ToolExample>? Examples => null;
    
    /// <inheritdoc />
    public ToolDetail GetDetail() => _toolDetail.Value;
    
    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // 调试日志：打印原始参数
            Console.WriteLine($"[ToolBase] Tool={Name}, Parameters.ValueKind={context.Parameters.ValueKind}");
            Console.WriteLine($"[ToolBase] Tool={Name}, RawArguments={context.RawArguments}");
            if (context.Parameters.ValueKind != JsonValueKind.Undefined)
            {
                Console.WriteLine($"[ToolBase] Tool={Name}, Parameters.GetRawText()={context.Parameters.GetRawText()}");
            }
            
            // 反序列化参数
            TParams parameters;
            try
            {
                parameters = DeserializeParameters(context.Parameters);
                Console.WriteLine($"[ToolBase] Tool={Name}, Deserialized parameters={JsonSerializer.Serialize(parameters, JsonSerializerOptions)}");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ToolBase] Tool={Name}, Deserialization failed: {ex.Message}");
                return ToolResult.Failure(
                    $"参数解析失败: {ex.Message}",
                    "INVALID_PARAMETERS",
                    isRetryable: true);
            }
            
            // 验证参数
            var validationErrors = ValidateParameters(parameters);
            if (validationErrors.Count > 0)
            {
                Console.WriteLine($"[ToolBase] Tool={Name}, Validation errors: {string.Join("; ", validationErrors)}");
                return ToolResult.Failure(
                    $"参数验证失败: {string.Join("; ", validationErrors)}",
                    "VALIDATION_FAILED",
                    isRetryable: true);
            }
            
            // 执行工具逻辑
            return await ExecuteAsync(parameters, context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Failure("操作已取消", "CANCELLED", isRetryable: false);
        }
        catch (Exception ex)
        {
            return ToolResult.FromException(ex);
        }
    }
    
    /// <summary>
    /// 执行工具逻辑 - 子类实现
    /// </summary>
    /// <param name="parameters">强类型参数</param>
    /// <param name="context">执行上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected abstract Task<ToolResult> ExecuteAsync(
        TParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// 反序列化参数
    /// </summary>
    protected virtual TParams DeserializeParameters(JsonElement parameters)
    {
        return parameters.Deserialize<TParams>(JsonSerializerOptions) ?? new TParams();
    }
    
    /// <summary>
    /// 验证参数
    /// </summary>
    protected virtual List<string> ValidateParameters(TParams parameters)
    {
        var errors = new List<string>();
        var validationContext = new ValidationContext(parameters);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
        {
            errors.AddRange(validationResults
                .Where(r => !string.IsNullOrEmpty(r.ErrorMessage))
                .Select(r => r.ErrorMessage!));
        }
        
        return errors;
    }
    
    private ToolDetail BuildToolDetail()
    {
        return new ToolDetail
        {
            Name = Name,
            Description = Description,
            ParameterSchema = _parameterSchema.Value,
            ReturnDescription = ReturnDescription,
            Examples = Examples
        };
    }
    
    private string GenerateParameterSchema()
    {
        var schema = SchemaGenerator.Generate<TParams>();
        return JsonSerializer.Serialize(schema, JsonSerializerOptions);
    }
    
    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    protected static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };
}

/// <summary>
/// JSON Schema 生成器
/// 基于类型和特性自动生成 JSON Schema
/// </summary>
internal static class SchemaGenerator
{
    public static Dictionary<string, object> Generate<T>() where T : class
    {
        return Generate(typeof(T));
    }
    
    public static Dictionary<string, object> Generate(Type type)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>(),
            ["required"] = new List<string>()
        };
        
        var properties = (Dictionary<string, object>)schema["properties"];
        var required = (List<string>)schema["required"];
        
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propName = GetPropertyName(prop);
            var propSchema = GeneratePropertySchema(prop);
            properties[propName] = propSchema;
            
            // 检查是否必需
            if (prop.GetCustomAttribute<RequiredAttribute>() != null)
            {
                required.Add(propName);
            }
        }
        
        return schema;
    }
    
    private static string GetPropertyName(PropertyInfo prop)
    {
        var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropAttr != null)
        {
            return jsonPropAttr.Name;
        }
        
        // 使用 snake_case
        return ToSnakeCase(prop.Name);
    }
    
    private static Dictionary<string, object> GeneratePropertySchema(PropertyInfo prop)
    {
        var schema = new Dictionary<string, object>();
        var propType = prop.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;
        
        // 获取描述
        var descAttr = prop.GetCustomAttribute<DescriptionAttribute>();
        if (descAttr != null)
        {
            schema["description"] = descAttr.Description;
        }
        
        // 处理类型
        if (underlyingType == typeof(string))
        {
            schema["type"] = "string";
            
            // 检查枚举值约束
            var allowedValues = prop.GetCustomAttribute<AllowedValuesAttribute>();
            if (allowedValues != null)
            {
                schema["enum"] = allowedValues.Values;
            }
        }
        else if (underlyingType == typeof(int) || underlyingType == typeof(long))
        {
            schema["type"] = "integer";
            
            var range = prop.GetCustomAttribute<RangeAttribute>();
            if (range != null)
            {
                schema["minimum"] = range.Minimum;
                schema["maximum"] = range.Maximum;
            }
        }
        else if (underlyingType == typeof(double) || underlyingType == typeof(float) || underlyingType == typeof(decimal))
        {
            schema["type"] = "number";
        }
        else if (underlyingType == typeof(bool))
        {
            schema["type"] = "boolean";
        }
        else if (underlyingType.IsEnum)
        {
            schema["type"] = "string";
            schema["enum"] = Enum.GetNames(underlyingType).Select(ToSnakeCase).ToArray();
        }
        else if (underlyingType.IsArray || (underlyingType.IsGenericType && 
                 underlyingType.GetGenericTypeDefinition() == typeof(List<>)))
        {
            schema["type"] = "array";
            var elementType = underlyingType.IsArray 
                ? underlyingType.GetElementType()! 
                : underlyingType.GetGenericArguments()[0];
            schema["items"] = GenerateTypeSchema(elementType);
        }
        else if (underlyingType.IsClass && underlyingType != typeof(string))
        {
            // 嵌套对象
            return Generate(underlyingType);
        }
        else
        {
            schema["type"] = "string";
        }
        
        // 处理默认值
        var defaultValue = prop.GetCustomAttribute<DefaultValueAttribute>();
        if (defaultValue?.Value != null)
        {
            schema["default"] = defaultValue.Value;
        }
        
        return schema;
    }
    
    private static Dictionary<string, object> GenerateTypeSchema(Type type)
    {
        var schema = new Dictionary<string, object>();
        
        if (type == typeof(string))
            schema["type"] = "string";
        else if (type == typeof(int) || type == typeof(long))
            schema["type"] = "integer";
        else if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            schema["type"] = "number";
        else if (type == typeof(bool))
            schema["type"] = "boolean";
        else if (type.IsEnum)
        {
            schema["type"] = "string";
            schema["enum"] = Enum.GetNames(type).Select(ToSnakeCase).ToArray();
        }
        else
            schema["type"] = "object";
        
        return schema;
    }
    
    private static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        
        var result = new System.Text.StringBuilder();
        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (char.IsUpper(c))
            {
                if (i > 0) result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}

/// <summary>
/// 允许的值特性（用于字符串枚举约束）
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AllowedValuesAttribute : Attribute
{
    public object[] Values { get; }
    
    public AllowedValuesAttribute(params object[] values)
    {
        Values = values;
    }
}
