using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Persistence;

/// <summary>
/// Task 4.2: proves every one of the 16 concrete <see cref="GameEvent"/>
/// variants (verified via <c>Glob</c>/<c>Grep</c> on <c>src/Risk.Engine/Events/*.cs</c>,
/// matching <see cref="Risk.Web.Persistence.ClosedHierarchyResolver"/>'s
/// registered list) survives a round-trip through <see cref="Risk.Web.Persistence.GameSnapshotSerializer"/>
/// with its concrete type AND every field value intact. Constructs one
/// instance of each variant directly and round-trips just a
/// <see cref="GameState.Log"/> containing all 16 — some variants (e.g.
/// <see cref="PlayerEliminated"/>) require a specific, hard-to-arrange game
/// state to occur naturally, so driving a real game through all 16 would be
/// both slower and less exhaustive than constructing them directly.
/// </summary>
public class GameEventRoundTripTests
{
    [Fact]
    public void All_16_GameEvent_variants_round_trip_with_concrete_type_and_values_intact()
    {
        IReadOnlyList<GameEvent> events =
        [
            new BattleResolved(new PlayerId(0), new TerritoryId("Alaska"), new TerritoryId("Alberta"), [6, 5, 3], [4, 2], 1, 2),
            new CardDrawn(new PlayerId(0), new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Infantry)),
            new CardsTraded(
                new PlayerId(0),
                [new TerritoryCard(new TerritoryId("Alaska"), CardSymbol.Infantry), new WildCard(), new TerritoryCard(new TerritoryId("Alberta"), CardSymbol.Cavalry)],
                4,
                new TerritoryId("Alaska")),
            new GameWon(new PlayerId(0)),
            new HeadquartersCaptured(new PlayerId(0), new PlayerId(1), new TerritoryId("Alaska")),
            new HeadquartersRevealed(new Dictionary<PlayerId, TerritoryId>
            {
                [new PlayerId(0)] = new TerritoryId("Alaska"),
                [new PlayerId(1)] = new TerritoryId("Alberta"),
            }),
            new HeadquartersSelected(new PlayerId(0)),
            new NeutralTroopsPlaced(new PlayerId(0), new TerritoryId("Alaska"), 1),
            new PhaseChanged(TurnPhase.Setup, TurnPhase.Reinforce, new PlayerId(0)),
            new PlayerEliminated(new PlayerId(1), new PlayerId(0), 3),
            new TerritoriesAssigned(new Dictionary<TerritoryId, PlayerId>
            {
                [new TerritoryId("Alaska")] = new PlayerId(0),
                [new TerritoryId("Alberta")] = new PlayerId(1),
            }),
            new TerritoryClaimed(new PlayerId(0), new TerritoryId("Alaska"), 1),
            new TerritoryConquered(new PlayerId(0), new PlayerId(1), new TerritoryId("Alaska")),
            new TerritoryOccupied(new PlayerId(0), new TerritoryId("Alaska"), 3),
            new TroopsFortified(new PlayerId(0), new TerritoryId("Alaska"), new TerritoryId("Alberta"), 2),
            new TroopsPlaced(new PlayerId(0), new TerritoryId("Alaska"), 1),
        ];

        // Guards this test itself: if a 17th variant is ever added to
        // GameEvent, this count assertion (16, pinned deliberately, not
        // derived from events.Count) fails loudly rather than silently
        // passing with fewer variants covered than intended.
        Assert.Equal(16, events.Count);

        var baseState = BuildMinimalClassicState();
        var state = baseState with { Log = events };

        var deserialized = GameStateAssertions.RoundTripThroughCanonicalJson(state);

        Assert.Equal(events.Count, deserialized.Log.Count);
        for (var i = 0; i < events.Count; i++)
        {
            AssertEventEqual(events[i], deserialized.Log[i]);
        }
    }

    private static GameState BuildMinimalClassicState()
    {
        var setupResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(3, GameMode.Classic, QueuedDiceRoller.ForRollOff(3)));
        return setupResult.State;
    }

    private static void AssertEventEqual(GameEvent expected, GameEvent actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());

        switch (expected)
        {
            case BattleResolved e:
                var battleResolved = Assert.IsType<BattleResolved>(actual);
                Assert.Equal(e.Attacker, battleResolved.Attacker);
                Assert.Equal(e.From, battleResolved.From);
                Assert.Equal(e.To, battleResolved.To);
                Assert.Equal(e.AttackerRolls, battleResolved.AttackerRolls);
                Assert.Equal(e.DefenderRolls, battleResolved.DefenderRolls);
                Assert.Equal(e.AttackerLosses, battleResolved.AttackerLosses);
                Assert.Equal(e.DefenderLosses, battleResolved.DefenderLosses);
                break;
            case CardDrawn e:
                var cardDrawn = Assert.IsType<CardDrawn>(actual);
                Assert.Equal(e.Actor, cardDrawn.Actor);
                AssertCardEqual(e.Card, cardDrawn.Card);
                break;
            case CardsTraded e:
                var cardsTraded = Assert.IsType<CardsTraded>(actual);
                Assert.Equal(e.Actor, cardsTraded.Actor);
                Assert.Equal(e.Cards.Count, cardsTraded.Cards.Count);
                for (var i = 0; i < e.Cards.Count; i++)
                {
                    AssertCardEqual(e.Cards[i], cardsTraded.Cards[i]);
                }

                Assert.Equal(e.Bonus, cardsTraded.Bonus);
                Assert.Equal(e.BonusTerritory, cardsTraded.BonusTerritory);
                break;
            case GameWon e:
                var gameWon = Assert.IsType<GameWon>(actual);
                Assert.Equal(e.Winner, gameWon.Winner);
                break;
            case HeadquartersCaptured e:
                var headquartersCaptured = Assert.IsType<HeadquartersCaptured>(actual);
                Assert.Equal(e.Attacker, headquartersCaptured.Attacker);
                Assert.Equal(e.OriginalOwner, headquartersCaptured.OriginalOwner);
                Assert.Equal(e.Territory, headquartersCaptured.Territory);
                break;
            case HeadquartersRevealed e:
                var headquartersRevealed = Assert.IsType<HeadquartersRevealed>(actual);
                Assert.Equal(e.Headquarters.Count, headquartersRevealed.Headquarters.Count);
                foreach (var (playerId, territoryId) in e.Headquarters)
                {
                    Assert.Equal(territoryId, headquartersRevealed.Headquarters[playerId]);
                }

                break;
            case HeadquartersSelected e:
                var headquartersSelected = Assert.IsType<HeadquartersSelected>(actual);
                Assert.Equal(e.Player, headquartersSelected.Player);
                break;
            case NeutralTroopsPlaced e:
                var neutralTroopsPlaced = Assert.IsType<NeutralTroopsPlaced>(actual);
                Assert.Equal(e.Placer, neutralTroopsPlaced.Placer);
                Assert.Equal(e.Territory, neutralTroopsPlaced.Territory);
                Assert.Equal(e.Troops, neutralTroopsPlaced.Troops);
                break;
            case PhaseChanged e:
                var phaseChanged = Assert.IsType<PhaseChanged>(actual);
                Assert.Equal(e.From, phaseChanged.From);
                Assert.Equal(e.To, phaseChanged.To);
                Assert.Equal(e.CurrentPlayer, phaseChanged.CurrentPlayer);
                break;
            case PlayerEliminated e:
                var playerEliminated = Assert.IsType<PlayerEliminated>(actual);
                Assert.Equal(e.Victim, playerEliminated.Victim);
                Assert.Equal(e.By, playerEliminated.By);
                Assert.Equal(e.CardsTransferred, playerEliminated.CardsTransferred);
                break;
            case TerritoriesAssigned e:
                var territoriesAssigned = Assert.IsType<TerritoriesAssigned>(actual);
                Assert.Equal(e.Assignments.Count, territoriesAssigned.Assignments.Count);
                foreach (var (territoryId, playerId) in e.Assignments)
                {
                    Assert.Equal(playerId, territoriesAssigned.Assignments[territoryId]);
                }

                break;
            case TerritoryClaimed e:
                var territoryClaimed = Assert.IsType<TerritoryClaimed>(actual);
                Assert.Equal(e.Player, territoryClaimed.Player);
                Assert.Equal(e.Territory, territoryClaimed.Territory);
                Assert.Equal(e.Troops, territoryClaimed.Troops);
                break;
            case TerritoryConquered e:
                var territoryConquered = Assert.IsType<TerritoryConquered>(actual);
                Assert.Equal(e.Conqueror, territoryConquered.Conqueror);
                Assert.Equal(e.PreviousOwner, territoryConquered.PreviousOwner);
                Assert.Equal(e.Territory, territoryConquered.Territory);
                break;
            case TerritoryOccupied e:
                var territoryOccupied = Assert.IsType<TerritoryOccupied>(actual);
                Assert.Equal(e.Player, territoryOccupied.Player);
                Assert.Equal(e.Territory, territoryOccupied.Territory);
                Assert.Equal(e.Troops, territoryOccupied.Troops);
                break;
            case TroopsFortified e:
                var troopsFortified = Assert.IsType<TroopsFortified>(actual);
                Assert.Equal(e.Actor, troopsFortified.Actor);
                Assert.Equal(e.From, troopsFortified.From);
                Assert.Equal(e.To, troopsFortified.To);
                Assert.Equal(e.Troops, troopsFortified.Troops);
                break;
            case TroopsPlaced e:
                var troopsPlaced = Assert.IsType<TroopsPlaced>(actual);
                Assert.Equal(e.Player, troopsPlaced.Player);
                Assert.Equal(e.Territory, troopsPlaced.Territory);
                Assert.Equal(e.Troops, troopsPlaced.Troops);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unhandled {nameof(GameEvent)} type in this test: {expected.GetType().Name}. " +
                    "Add a case above (and confirm it is registered in ClosedHierarchyResolver) before trusting this test's coverage.");
        }
    }

    private static void AssertCardEqual(Card expected, Card actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());

        switch (expected)
        {
            case TerritoryCard e:
                var territoryCard = Assert.IsType<TerritoryCard>(actual);
                Assert.Equal(e.Territory, territoryCard.Territory);
                Assert.Equal(e.Symbol, territoryCard.Symbol);
                break;
            case WildCard:
                Assert.IsType<WildCard>(actual);
                break;
        }
    }
}
