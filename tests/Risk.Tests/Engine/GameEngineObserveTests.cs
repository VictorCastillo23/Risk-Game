using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

public class GameEngineObserveTests
{
    [Fact]
    public void Observe_reveals_the_viewers_own_hand()
    {
        var state = BuildTwoPlayerState();
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(0));

        Assert.Equal(2, view.OwnHand.Count);
    }

    [Fact]
    public void Observe_redacts_other_players_hands_to_card_counts()
    {
        var state = BuildTwoPlayerState();
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(0));

        Assert.Equal(3, view.OtherPlayersCardCounts[new PlayerId(1)]);
        Assert.False(view.OtherPlayersCardCounts.ContainsKey(new PlayerId(0)));
    }

    [Fact]
    public void Observe_is_symmetric_for_a_different_viewer()
    {
        var state = BuildTwoPlayerState();
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(1));

        Assert.Equal(3, view.OwnHand.Count);
        Assert.Equal(2, view.OtherPlayersCardCounts[new PlayerId(0)]);
    }

    [Fact]
    public void Observe_reveals_the_viewers_own_effective_mission()
    {
        var mission = new OccupyTerritories(24, MinArmiesPerTerritory: 1);
        var state = BuildSecretMissionState(mission, otherMission: null);
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(0));

        Assert.Equal(mission, view.OwnEffectiveMission);
    }

    [Fact]
    public void Observe_substitutes_a_self_targeting_EliminateArmy_into_the_own_effective_mission()
    {
        var selfMission = new EliminateArmy(new ArmyId(0));
        var state = BuildSecretMissionState(selfMission, otherMission: null);
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(0));

        Assert.Equal(new OccupyTerritories(24, MinArmiesPerTerritory: 1), view.OwnEffectiveMission);
    }

    [Fact]
    public void Observe_own_effective_mission_is_null_outside_SecretMission()
    {
        var state = BuildTwoPlayerState();
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(0));

        Assert.Null(view.OwnEffectiveMission);
    }

    [Fact]
    public void Observe_never_exposes_another_players_mission()
    {
        var ownMission = new OccupyTerritories(24, MinArmiesPerTerritory: 1);
        var otherMission = new ConquerContinents(Required: [new ContinentId("NA")], WildcardCount: 0);
        var state = BuildSecretMissionState(ownMission, otherMission);
        var engine = new GameEngine(new QueuedDiceRoller());

        var view = engine.Observe(state, new PlayerId(0));

        Assert.Equal(ownMission, view.OwnEffectiveMission);
        Assert.NotEqual(otherMission, view.OwnEffectiveMission);
    }

    private static GameState BuildTwoPlayerState()
    {
        IReadOnlyList<Card> player0Hand = [new WildCard(), new WildCard()];
        IReadOnlyList<Card> player1Hand = [new WildCard(), new WildCard(), new WildCard()];

        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(new PlayerId(0), player0Hand, false, 0),
            new PlayerState(new PlayerId(1), player1Hand, false, 0)
        ];

        return new GameState(
            new Dictionary<TerritoryId, TerritoryState>(),
            players,
            new TurnState(new PlayerId(0), TurnPhase.Reinforce),
            Deck.CreateStandard(),
            [],
            new GameStatus.InProgress());
    }

    private static GameState BuildSecretMissionState(MissionCard ownMission, MissionCard? otherMission)
    {
        IReadOnlyList<PlayerState> players =
        [
            new PlayerState(new PlayerId(0), [], false, 0, Mission: ownMission),
            new PlayerState(new PlayerId(1), [], false, 0, Mission: otherMission)
        ];

        return new GameState(
            new Dictionary<TerritoryId, TerritoryState>(),
            players,
            new TurnState(new PlayerId(0), TurnPhase.Reinforce),
            Deck.CreateStandard(),
            [],
            new GameStatus.InProgress(),
            Mode: GameMode.SecretMission);
    }
}
