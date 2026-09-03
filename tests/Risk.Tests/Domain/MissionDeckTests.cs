using System.Reflection;
using Risk.Domain.Map;
using Risk.Domain.Missions;

namespace Risk.Tests.Domain;

public class MissionDeckTests
{
    [Fact]
    public void CreateStandard_returns_14_cards()
    {
        var deck = MissionDeck.CreateStandard();

        Assert.Equal(14, deck.Count);
    }

    [Fact]
    public void CreateStandard_has_exactly_one_of_each_occupy_variant()
    {
        var deck = MissionDeck.CreateStandard();
        var occupyCards = deck.OfType<OccupyTerritories>().ToList();

        Assert.Equal(2, occupyCards.Count);
        Assert.Equal(1, occupyCards.Count(c => c.Count == 18 && c.MinArmiesPerTerritory == 2));
        Assert.Equal(1, occupyCards.Count(c => c.Count == 24 && c.MinArmiesPerTerritory == 1));
    }

    [Fact]
    public void CreateStandard_has_exactly_six_eliminate_army_cards_with_distinct_ids()
    {
        var deck = MissionDeck.CreateStandard();
        var armyIds = deck.OfType<EliminateArmy>().Select(c => c.Army).ToList();

        Assert.Equal(6, armyIds.Count);
        Assert.Equal(armyIds.Count, armyIds.Distinct().Count());
        Assert.Equal(
            [0, 1, 2, 3, 4, 5],
            armyIds.Select(a => a.Value).OrderBy(v => v));
    }

    [Fact]
    public void CreateStandard_has_exactly_six_conquer_continents_cards_with_exact_sets()
    {
        var deck = MissionDeck.CreateStandard();
        var conquerCards = deck.OfType<ConquerContinents>().ToList();

        Assert.Equal(6, conquerCards.Count);

        // Project to a comparable value: IReadOnlyList<ContinentId> has
        // reference equality by default, so a naive Distinct()/Equals check
        // would vacuously pass even with duplicated continent sets.
        var projected = conquerCards
            .Select(c => (Set: string.Join("+", c.Required.Select(id => id.Value)), c.WildcardCount))
            .ToList();

        Assert.Equal(6, projected.Distinct().Count());

        var fixedPairs = projected.Where(p => p.WildcardCount == 0).Select(p => p.Set).OrderBy(s => s);
        Assert.Equal(
            new[] { "AS+AF", "AS+SA", "NA+AF", "NA+OC" }.OrderBy(s => s),
            fixedPairs);

        var wildcardPairs = projected.Where(p => p.WildcardCount == 1).Select(p => p.Set).OrderBy(s => s);
        Assert.Equal(
            new[] { "EU+OC", "EU+SA" }.OrderBy(s => s),
            wildcardPairs);
    }

    [Fact]
    public void CreateStandard_references_only_real_continent_ids()
    {
        var deck = MissionDeck.CreateStandard();
        var knownIds = Continents.All.Select(c => c.Id).ToHashSet();

        foreach (var card in deck.OfType<ConquerContinents>())
        {
            foreach (var continentId in card.Required)
            {
                Assert.Contains(continentId, knownIds);
            }
        }
    }

    [Fact]
    public void CreateStandard_is_deterministic_and_unshuffled()
    {
        var first = MissionDeck.CreateStandard();
        var second = MissionDeck.CreateStandard();

        Assert.Equal(first.Count, second.Count);

        for (var i = 0; i < first.Count; i++)
        {
            if (first[i] is ConquerContinents a && second[i] is ConquerContinents b)
            {
                Assert.Equal(a.Required, b.Required);
                Assert.Equal(a.WildcardCount, b.WildcardCount);
            }
            else
            {
                Assert.Equal(first[i], second[i]);
            }
        }
    }

    [Fact]
    public void MissionDeck_has_no_reference_to_the_Cards_namespace()
    {
        var missionTypes = typeof(MissionCard).Assembly.GetTypes()
            .Where(t => t.Namespace == "Risk.Domain.Missions");

        foreach (var type in missionTypes)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var member in members)
            {
                var referencedTypes = member switch
                {
                    PropertyInfo p => [p.PropertyType],
                    FieldInfo f => new[] { f.FieldType },
                    MethodInfo m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType).ToArray(),
                    ConstructorInfo c => c.GetParameters().Select(p => p.ParameterType).ToArray(),
                    _ => Array.Empty<Type>()
                };

                foreach (var referenced in referencedTypes)
                {
                    Assert.NotEqual("Risk.Domain.Cards", referenced.Namespace);
                }
            }
        }
    }
}
