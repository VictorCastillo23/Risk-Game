using System.Reflection;

namespace Risk.AI.Tests.Bot;

/// <summary>
/// Design D1's structural, test-enforced boundary: <c>Risk.AI</c> is allowed
/// to name <see cref="Risk.Engine.State.GameState"/>/<see cref="Risk.Engine.State.PlayerState"/>
/// in exactly three types — <c>BotTurnRunner</c>, <c>BotRunResult</c>, and
/// <c>BotTurnStep</c> (all Phase 8). <c>BotTurnStep</c> was added post-Phase-8
/// (a resilience-review follow-up) as a shared observe/decide/execute/fold
/// primitive extracted so <c>BotTurnRunner</c>'s production loop and
/// <c>Risk.AI.Tests</c>' <c>GameHarness</c> fixture never silently drift
/// apart — it is pure orchestration plumbing, exactly like <c>BotTurnRunner</c>
/// itself, and never exposes <c>GameState</c>/<c>PlayerState</c> to an
/// <see cref="IBotPlayer"/> any more than <c>BotTurnRunner</c> already did.
/// Every other member — public or internal, method, property, field, or
/// constructor, anywhere in its signature including generic type arguments —
/// MUST NOT reference either type. This is the assertion the design calls
/// for instead of relying on convention/code-review alone to keep the
/// no-hidden-information contract real.
/// </summary>
public class NoHiddenInformationBoundaryTests
{
    private static readonly HashSet<string> ExemptTypeNames = ["BotTurnRunner", "BotRunResult", "BotTurnStep"];

    private static readonly HashSet<string> ForbiddenFullNames =
    [
        "Risk.Engine.State.GameState",
        "Risk.Engine.State.PlayerState",
    ];

    [Fact]
    public void No_member_outside_BotTurnRunner_BotRunResult_or_BotTurnStep_references_GameState_or_PlayerState()
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

        Assert.True(violations.Count == 0, "Found GameState/PlayerState references outside BotTurnRunner/BotRunResult/BotTurnStep:\n" + string.Join('\n', violations));
    }

    /// <summary>
    /// A type is exempt if it IS one of the three allowed types, or is nested
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
