using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Course.RuntimeLab;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class NonBlankAttribute : Attribute;

public sealed class ImportOptions
{
    [NonBlank]
    public string? Name { get; init; }
}

public static class MetadataValidator
{
    // The declared T defines the schema; derived-only properties are not inspected.
    public static IReadOnlyList<string> Validate<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T item)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(item);
        var errors = new List<string>();
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (property.GetCustomAttribute<NonBlankAttribute>() is null) continue;
            if (property.PropertyType != typeof(string) ||
                property.GetIndexParameters().Length != 0 ||
                property.GetMethod is not { IsPublic: true })
                throw new InvalidOperationException($"Unsupported property: {property.Name}");
            if (string.IsNullOrWhiteSpace((string?)property.GetValue(item)))
                errors.Add(property.Name);
        }
        return errors.AsReadOnly();
    }

    public static void Require(bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition) throw new ArgumentException(expression);
    }
}
