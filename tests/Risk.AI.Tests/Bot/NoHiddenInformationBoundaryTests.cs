using System.Reflection;

namespace Risk.AI.Tests.Bot;

/// <summary>
/// Design D1's structural, test-enforced boundary: <c>Risk.AI</c> is allowed
/// to name <see cref="Risk.Engine.State.GameState"/>/<see cref="Risk.Engine.State.PlayerState"/>
/// in exactly two types — <c>BotTurnRunner</c> and <c>BotRunResult</c> (both
/// Phase 8; neither exists yet in this phase, so the exclusion list below is
/// forward-compatible rather than currently exercised). Every other member —
/// public or internal, method, property, field, or constructor, anywhere in
/// its signature including generic type arguments — MUST NOT reference
/// either type. This is the assertion the design calls for instead of
/// relying on convention/code-review alone to keep the no-hidden-information
/// contract real.
/// </summary>
public class NoHiddenInformationBoundaryTests
{
    private static readonly HashSet<string> ExemptTypeNames = ["BotTurnRunner", "BotRunResult"];

    private static readonly HashSet<string> ForbiddenFullNames =
    [
        "Risk.Engine.State.GameState",
        "Risk.Engine.State.PlayerState",
    ];

    [Fact]
    public void No_member_outside_BotTurnRunner_or_BotRunResult_references_GameState_or_PlayerState()
    {
        var assembly = typeof(IBotPlayer).Assembly;
        var violations = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            if (IsExempt(type))
            {
                continue;
            }

            foreach (var member in DescribeMembers(type))
            {
                violations.Add(member);
            }
        }

        Assert.True(violations.Count == 0, "Found GameState/PlayerState references outside BotTurnRunner/BotRunResult:\n" + string.Join('\n', violations));
    }

    /// <summary>
    /// A type is exempt if it IS one of the two allowed types, or is nested
    /// inside one (e.g. BotRunResult's case records: Completed/Rejected/Exhausted).
    /// </summary>
    private static bool IsExempt(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (ExemptTypeNames.Contains(current.Name))
            {
                return true;
            }
        }

        return false;
    }

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static IEnumerable<string> DescribeMembers(Type type)
    {
        foreach (var ctor in type.GetConstructors(AllDeclared))
        {
            foreach (var parameter in ctor.GetParameters())
            {
                if (ReferencesForbiddenType(parameter.ParameterType))
                {
                    yield return $"{type.FullName}..ctor(...{parameter.ParameterType}...)";
                }
            }
        }

        foreach (var method in type.GetMethods(AllDeclared))
        {
            if (method.IsSpecialName)
            {
                // Skips property/event accessors; properties/fields are checked directly below.
                continue;
            }

            if (ReferencesForbiddenType(method.ReturnType))
            {
                yield return $"{type.FullName}.{method.Name}() return type {method.ReturnType}";
            }

            foreach (var parameter in method.GetParameters())
            {
                if (ReferencesForbiddenType(parameter.ParameterType))
                {
                    yield return $"{type.FullName}.{method.Name}(...{parameter.ParameterType}...)";
                }
            }
        }

        foreach (var property in type.GetProperties(AllDeclared))
        {
            if (ReferencesForbiddenType(property.PropertyType))
            {
                yield return $"{type.FullName}.{property.Name} : {property.PropertyType}";
            }
        }

        foreach (var field in type.GetFields(AllDeclared))
        {
            if (field.IsSpecialName)
            {
                continue;
            }

            if (ReferencesForbiddenType(field.FieldType))
            {
                yield return $"{type.FullName}.{field.Name} : {field.FieldType}";
            }
        }
    }

    /// <summary>
    /// True if <paramref name="type"/> IS a forbidden type, or reaches one
    /// through any generic type argument (e.g. <c>IReadOnlyDictionary&lt;PlayerId, GameState&gt;</c>),
    /// array element type, or by-ref element type (<see langword="out"/>/<see langword="ref"/> parameters).
    /// </summary>
    private static bool ReferencesForbiddenType(Type type)
    {
        if (type.FullName is { } fullName && ForbiddenFullNames.Contains(fullName))
        {
            return true;
        }

        if (type.HasElementType && ReferencesForbiddenType(type.GetElementType()!))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                if (ReferencesForbiddenType(argument))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
