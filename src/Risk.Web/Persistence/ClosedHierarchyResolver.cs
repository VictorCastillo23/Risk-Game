using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Risk.Domain.Cards;
using Risk.Domain.Missions;
using Risk.Engine.Events;
using Risk.Engine.State;

namespace Risk.Web.Persistence;

/// <summary>
/// Attaches a <c>$kind</c>-style discriminator to System.Text.Json's
/// polymorphism support for this codebase's 4 closed hierarchies
/// (<see cref="Card"/>: 2 variants, <see cref="GameStatus"/>: 2 variants,
/// <see cref="MissionCard"/>: 3 variants, <see cref="GameEvent"/>: 16
/// variants), via a <see cref="System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver"/>
/// modifier instead of <c>[JsonDerivedType]</c> attributes (design D3) —
/// attributes on the base records would require touching
/// <c>Risk.Domain</c>/<c>Risk.Engine</c>, which this change must not do.
/// <see cref="MissionCard"/>'s <c>private protected</c> constructor is not
/// an obstacle: System.Text.Json constructs concrete derived records via
/// their own public constructors through reflection, not through this
/// resolver, and never needs to call the abstract base's constructor
/// directly itself.
/// </summary>
public static class ClosedHierarchyResolver
{
    private const string DiscriminatorPropertyName = "$kind";

    public static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(Card))
        {
            Attach(typeInfo, typeof(TerritoryCard), typeof(WildCard));
        }
        else if (typeInfo.Type == typeof(GameStatus))
        {
            Attach(typeInfo, typeof(GameStatus.InProgress), typeof(GameStatus.Won));
        }
        else if (typeInfo.Type == typeof(MissionCard))
        {
            Attach(typeInfo, typeof(ConquerContinents), typeof(EliminateArmy), typeof(OccupyTerritories));
        }
        else if (typeInfo.Type == typeof(GameEvent))
        {
            Attach(
                typeInfo,
                typeof(BattleResolved),
                typeof(CardDrawn),
                typeof(CardsTraded),
                typeof(GameWon),
                typeof(HeadquartersCaptured),
                typeof(HeadquartersRevealed),
                typeof(HeadquartersSelected),
                typeof(NeutralTroopsPlaced),
                typeof(PhaseChanged),
                typeof(PlayerEliminated),
                typeof(TerritoriesAssigned),
                typeof(TerritoryClaimed),
                typeof(TerritoryConquered),
                typeof(TerritoryOccupied),
                typeof(TroopsFortified),
                typeof(TroopsPlaced));
        }
    }

    private static void Attach(JsonTypeInfo typeInfo, params Type[] derivedTypes)
    {
        var polymorphism = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = DiscriminatorPropertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
        };

        foreach (var derivedType in derivedTypes)
        {
            polymorphism.DerivedTypes.Add(new JsonDerivedType(derivedType, derivedType.Name));
        }

        typeInfo.PolymorphismOptions = polymorphism;
    }
}
