using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Web.Models;
using Risk.Web.Persistence;
using Risk.Web.Tests.Fakes;

namespace Risk.Web.Tests.Persistence;

/// <summary>
/// Task 3.1 (RED-first): proves <see cref="GameSnapshotSerializer"/> can
/// round-trip a real, in-progress <see cref="GameMode.Classic"/> game.
/// Deliberately does NOT assert <c>deserialized == original</c> or
/// <c>deserialized.Equals(original)</c> — <see cref="GameState"/>'s
/// record-generated <c>Equals</c> falls back to reference equality on its
/// <c>IReadOnlyDictionary</c>/<c>IReadOnlyList</c> members, so that
/// comparison is false even on a perfect round-trip. Correctness is proven
/// via canonical-JSON re-serialize equality plus targeted structural
/// spot-checks instead (per the design's documented gotcha).
/// </summary>
public class GameSnapshotSerializerTests
{
    [Fact]
    public void Classic_game_state_round_trips_through_canonical_JSON_equality()
    {
        var state = BuildInProgressClassicGame();

        var json = GameSnapshotSerializer.SerializeState(state);
        var deserialized = GameSnapshotSerializer.DeserializeState(json);
        var reserializedJson = GameSnapshotSerializer.SerializeState(deserialized);

        Assert.Equal(json, reserializedJson);

        Assert.Equal(state.Territories.Count, deserialized.Territories.Count);
        foreach (var (territoryId, territoryState) in state.Territories)
        {
            var roundTripped = deserialized.Territories[territoryId];
            Assert.Equal(territoryState.Owner, roundTripped.Owner);
            Assert.Equal(territoryState.Troops, roundTripped.Troops);
        }

        Assert.Equal(state.Players.Count, deserialized.Players.Count);
        for (var i = 0; i < state.Players.Count; i++)
        {
            Assert.Equal(state.Players[i].Id, deserialized.Players[i].Id);
            Assert.Equal(state.Players[i].TroopsRemaining, deserialized.Players[i].TroopsRemaining);
            Assert.Equal(state.Players[i].IsEliminated, deserialized.Players[i].IsEliminated);
        }

        Assert.Equal(state.Turn.CurrentPlayer, deserialized.Turn.CurrentPlayer);
        Assert.Equal(state.Turn.Phase, deserialized.Turn.Phase);
        Assert.Equal(state.Deck.Count, deserialized.Deck.Count);
        Assert.Equal(state.Log.Count, deserialized.Log.Count);
        Assert.IsType<GameStatus.InProgress>(deserialized.Status);
        Assert.Equal(GameMode.Classic, deserialized.Mode);
    }

    [Fact]
    public void Player_config_list_round_trips_through_canonical_JSON_equality()
    {
        var players = new List<PlayerConfig>
        {
            new(new PlayerId(0), "Ana", "#E53935", false),
            new(new PlayerId(1), "Beto", "#1E88E5", false),
            new(new PlayerId(2), "Caro", "#43A047", true),
        };

        var json = GameSnapshotSerializer.SerializePlayers(players);
        var deserialized = GameSnapshotSerializer.DeserializePlayers(json);
        var reserializedJson = GameSnapshotSerializer.SerializePlayers(deserialized);

        Assert.Equal(json, reserializedJson);
        Assert.Equal(players.Count, deserialized.Count);
        for (var i = 0; i < players.Count; i++)
        {
            Assert.Equal(players[i].Id, deserialized[i].Id);
            Assert.Equal(players[i].Name, deserialized[i].Name);
            Assert.Equal(players[i].ColorHex, deserialized[i].ColorHex);
            Assert.Equal(players[i].IsAi, deserialized[i].IsAi);
        }
    }

    /// <summary>
    /// Drives a real 3-player <see cref="GameMode.Classic"/> game through
    /// Claim and Setup into Reinforce, mirroring
    /// <c>Risk.Tests.Fakes.GameStateBuilder</c>'s style (that type is
    /// <c>internal</c> to a different test assembly and cannot be reused
    /// directly here), so the round-trip exercises realistic, non-empty
    /// data (all 42 territories owned, starting troops placed) instead of a
    /// hand-built <see cref="GameState"/>.
    /// </summary>
    private static GameState BuildInProgressClassicGame()
    {
        var setupResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(3, GameMode.Classic, QueuedDiceRoller.ForRollOff(3)));
        var state = setupResult.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        while (state.Turn.Phase == TurnPhase.Claim)
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner is null).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new ClaimTerritoryCommand(actor, territory, 1)));
            state = result.State;
        }

        while (state.Turn.Phase == TurnPhase.Setup)
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1)));
            state = result.State;
        }

        return state;
    }

    /// <summary>
    /// Task 4.1: <see cref="GameMode.SecretMission"/> round-trip. Unlike
    /// Classic, this mode's <see cref="GameSetup.Create"/> already deals every
    /// territory and every player's <see cref="PlayerState.Mission"/> up
    /// front (no Claim phase), so driving straight through Setup into
    /// Reinforce is enough to reach a realistic in-progress state. Asserts
    /// each player's <c>Mission</c> (a <see cref="MissionCard"/> closed-
    /// hierarchy value) survives the round-trip with its concrete type and
    /// fields intact, on top of the canonical-JSON equality check.
    /// </summary>
    [Fact]
    public void SecretMission_game_state_round_trips_through_canonical_JSON_equality()
    {
        var setupResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(3, GameMode.SecretMission, new QueuedDiceRoller()));
        var state = setupResult.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        state = DriveSetupPhase(engine, state);

        Assert.All(state.Players, p => Assert.NotNull(p.Mission));
        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Equal(GameMode.SecretMission, state.Mode);

        var deserialized = GameStateAssertions.RoundTripThroughCanonicalJson(state);
        GameStateAssertions.AssertStructurallyEqual(state, deserialized);

        Assert.Equal(GameMode.SecretMission, deserialized.Mode);
        for (var i = 0; i < state.Players.Count; i++)
        {
            Assert.NotNull(deserialized.Players[i].Mission);
            Assert.Equal(state.Players[i].Mission!.GetType(), deserialized.Players[i].Mission!.GetType());
        }
    }

    /// <summary>
    /// Task 4.1: <see cref="GameMode.TwoPlayer"/> round-trip. Drives both
    /// Setup Phase A (the two real humans placing their own starting troops,
    /// 2 per turn) and Phase B (the humans placing the third, engine-created
    /// neutral army's troops via <see cref="PlaceNeutralTroopsCommand"/>,
    /// which fires <see cref="NeutralTroopsPlaced"/>) all the way into
    /// Reinforce, so the neutral <see cref="PlayerState.IsNeutral"/> flag,
    /// its owned territories, and its remaining troop pool are all
    /// exercised, not just declared.
    /// </summary>
    [Fact]
    public void TwoPlayer_game_state_round_trips_through_canonical_JSON_equality()
    {
        var setupResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, new QueuedDiceRoller()));
        var state = setupResult.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        state = DriveSetupPhase(engine, state);

        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Equal(GameMode.TwoPlayer, state.Mode);
        Assert.Equal(3, state.Players.Count);
        Assert.Single(state.Players, p => p.IsNeutral);
        Assert.Contains(state.Log, e => e is NeutralTroopsPlaced);

        var deserialized = GameStateAssertions.RoundTripThroughCanonicalJson(state);
        GameStateAssertions.AssertStructurallyEqual(state, deserialized);

        Assert.Equal(GameMode.TwoPlayer, deserialized.Mode);
        Assert.Single(deserialized.Players, p => p.IsNeutral);
        Assert.Contains(deserialized.Log, e => e is NeutralTroopsPlaced);
    }

    /// <summary>
    /// Task 4.1 (mandatory follow-up from PR3's review): <see cref="GameMode.Capital"/>
    /// round-trip that actually reaches <see cref="TurnPhase.SelectHeadquarters"/>
    /// and fires <see cref="HeadquartersRevealed"/> — the ONLY place in this
    /// codebase <see cref="PlayerId"/> is used as a <c>Dictionary</c> key
    /// (<see cref="HeadquartersRevealed.Headquarters"/>), so this is the
    /// first real exercise of <see cref="PlayerIdJsonConverter.ReadAsPropertyName"/>/
    /// <see cref="PlayerIdJsonConverter.WriteAsPropertyName"/>. Drives Claim,
    /// then Setup, then has every player select a headquarters (round-robin,
    /// same shape as Claim), then asserts the round-tripped
    /// <c>HeadquartersRevealed</c> event's dictionary has the exact same
    /// keys/values as the one recorded while driving the game.
    /// </summary>
    [Fact]
    public void Capital_game_state_round_trips_and_HeadquartersRevealed_dictionary_keys_survive()
    {
        var setupResult = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(3, GameMode.Capital, QueuedDiceRoller.ForRollOff(3)));
        var state = setupResult.State;
        var engine = new GameEngine(new QueuedDiceRoller());

        while (state.Turn.Phase == TurnPhase.Claim)
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner is null).Key;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new ClaimTerritoryCommand(actor, territory, 1)));
            state = result.State;
        }

        state = DriveSetupPhase(engine, state);

        Assert.Equal(TurnPhase.SelectHeadquarters, state.Turn.Phase);

        var expectedHeadquarters = new Dictionary<PlayerId, TerritoryId>();
        while (state.Turn.Phase == TurnPhase.SelectHeadquarters)
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
            expectedHeadquarters[actor] = territory;
            var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                engine.Execute(state, new SelectHeadquartersCommand(actor, territory)));
            state = result.State;
        }

        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Equal(GameMode.Capital, state.Mode);
        var revealed = Assert.Single(state.Log.OfType<HeadquartersRevealed>());
        Assert.Equal(expectedHeadquarters.Count, revealed.Headquarters.Count);
        foreach (var (playerId, territoryId) in expectedHeadquarters)
        {
            Assert.Equal(territoryId, revealed.Headquarters[playerId]);
        }
        Assert.All(state.Players, p => Assert.NotNull(p.HeadquartersId));

        var deserialized = GameStateAssertions.RoundTripThroughCanonicalJson(state);
        GameStateAssertions.AssertStructurallyEqual(state, deserialized);

        var deserializedRevealed = Assert.Single(deserialized.Log.OfType<HeadquartersRevealed>());
        Assert.Equal(expectedHeadquarters.Count, deserializedRevealed.Headquarters.Count);
        foreach (var (playerId, territoryId) in expectedHeadquarters)
        {
            Assert.Equal(territoryId, deserializedRevealed.Headquarters[playerId]);
        }

        Assert.All(deserialized.Players, p => Assert.NotNull(p.HeadquartersId));
    }

    /// <summary>
    /// Shared Setup-phase driver for the round-trip tests above. Handles
    /// every mode uniformly: while the current player still has troops of
    /// their own left, places <see cref="GameMode.TwoPlayer"/>'s 2-per-turn
    /// (or every other mode's 1-per-turn) budget on one of their territories
    /// via <see cref="PlaceTroopsCommand"/>; once a <see cref="GameMode.TwoPlayer"/>
    /// human's own pool is drained and the engine has rotated them into Phase
    /// B (per <c>GameEngine.AdvanceAfterSetupPlacement</c>, only a non-neutral
    /// player with <c>TroopsRemaining == 0</c> reaches this branch), places
    /// one of the neutral's troops via <see cref="PlaceNeutralTroopsCommand"/>
    /// instead. Stops once <c>Turn.Phase</c> is no longer <see cref="TurnPhase.Setup"/>
    /// (Reinforce for every mode except Capital, which lands on
    /// <see cref="TurnPhase.SelectHeadquarters"/>).
    /// </summary>
    private static GameState DriveSetupPhase(GameEngine engine, GameState state)
    {
        while (state.Turn.Phase == TurnPhase.Setup)
        {
            var actor = state.Turn.CurrentPlayer;
            var actorPlayer = state.Players.Single(p => p.Id == actor);

            if (actorPlayer.TroopsRemaining > 0)
            {
                var perTurn = state.Mode == GameMode.TwoPlayer ? 2 : 1;
                var budget = actorPlayer.TroopsRemaining % perTurn == 0
                    ? perTurn
                    : actorPlayer.TroopsRemaining % perTurn;
                var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;
                var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                    engine.Execute(state, new PlaceTroopsCommand(actor, territory, budget)));
                state = result.State;
            }
            else
            {
                var neutral = state.Players.Single(p => p.IsNeutral);
                var neutralTerritory = state.Territories.First(kv => kv.Value.Owner == neutral.Id).Key;
                var result = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
                    engine.Execute(state, new PlaceNeutralTroopsCommand(actor, neutralTerritory, 1)));
                state = result.State;
            }
        }

        return state;
    }
}
