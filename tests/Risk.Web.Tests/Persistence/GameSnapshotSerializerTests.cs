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
}
