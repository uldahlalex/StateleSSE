using System.Reflection;

namespace StateleSSE.AspNetCore;

/// <summary>
/// Provides methods to discover BaseResponseDto-derived event types and string constants
/// for OpenAPI documentation. Use with your OpenAPI generator's extension mechanism.
/// </summary>
public static class StringConstantsDiscovery
{
    /// <summary>
    /// Gets names of all non-abstract types derived from BaseResponseDto (event types).
    /// </summary>
    /// <param name="assemblies">Assemblies to scan. Defaults to all loaded assemblies.</param>
    public static IReadOnlyList<string> GetEventTypeNames(IEnumerable<Assembly>? assemblies = null)
    {
        assemblies ??= AppDomain.CurrentDomain.GetAssemblies();

        return assemblies
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .Where(t =>
                t != typeof(BaseResponseDto) &&
                !t.IsAbstract &&
                typeof(BaseResponseDto).IsAssignableFrom(t))
            .Select(t => t.Name)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Gets all public const string fields from the specified type.
    /// </summary>
    public static IReadOnlyList<string> GetStringConstants<T>() => GetStringConstants(typeof(T));

    /// <summary>
    /// Gets all public const string fields from the specified type.
    /// </summary>
    public static IReadOnlyList<string> GetStringConstants(Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => f.GetValue(null)?.ToString())
            .Where(c => c != null)
            .Distinct()
            .ToList()!;
    }

    /// <summary>
    /// Gets all event type names and string constants from the specified type, combined.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for event types. Defaults to all loaded assemblies.</param>
    public static IReadOnlyList<string> GetAll<TStringConstants>(IEnumerable<Assembly>? assemblies = null)
    {
        return GetEventTypeNames(assemblies)
            .Concat(GetStringConstants<TStringConstants>())
            .Distinct()
            .ToList();
    }
}
