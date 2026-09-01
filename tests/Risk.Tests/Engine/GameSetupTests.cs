using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.Events;
using Risk.Engine.Results;
using Risk.Engine.Setup;
using Risk.Engine.State;
using Risk.Tests.Fakes;

namespace Risk.Tests.Engine;

public class GameSetupTests
{
    public static IEnumerable<object[]> AllModes() =>
        Enum.GetValues<GameMode>().Select(mode => new object[] { mode });

    [Fact]
    public void Create_rejects_two_players_outside_TwoPlayer_mode()
    {
        var result = GameSetup.Create(2, GameMode.Classic, QueuedDiceRoller.ForRollOff(2));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Create_rejects_six_players_in_every_mode(GameMode mode)
    {
        // Six exceeds every mode's legal range, so Create rejects before
        // ever touching dice — a fresh empty roller is safe here even
        // though ForRollOff only supports up to 5 players.
        var result = GameSetup.Create(6, mode, new QueuedDiceRoller());

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer, 2)]
    [InlineData(GameMode.SecretMission, 3)]
    [InlineData(GameMode.SecretMission, 4)]
    [InlineData(GameMode.SecretMission, 5)]
    [InlineData(GameMode.Classic, 3)]
    [InlineData(GameMode.Classic, 4)]
    [InlineData(GameMode.Classic, 5)]
    [InlineData(GameMode.Capital, 3)]
    [InlineData(GameMode.Capital, 4)]
    [InlineData(GameMode.Capital, 5)]
    public void Create_accepts_only_the_legal_player_counts_for_each_mode(GameMode mode, int playerCount)
    {
        var result = GameSetup.Create(playerCount, mode, QueuedDiceRoller.ForRollOff(playerCount));

        Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer, 1)]
    [InlineData(GameMode.TwoPlayer, 3)]
    [InlineData(GameMode.Classic, 1)]
    [InlineData(GameMode.Classic, 2)]
    [InlineData(GameMode.Classic, 7)]
    [InlineData(GameMode.SecretMission, 2)]
    [InlineData(GameMode.Capital, 2)]
    public void Create_rejects_illegal_player_counts_for_each_mode(GameMode mode, int playerCount)
    {
        // Every row here is rejected before dice is ever rolled, including
        // the 7-player row that would overflow ForRollOff's 5-player queue.
        var result = GameSetup.Create(playerCount, mode, new QueuedDiceRoller());

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidPlayerCount, rejection.Error.Code);
    }

    [Theory]
    [InlineData(GameMode.Classic, 2)]
    [InlineData(GameMode.TwoPlayer, 3)]
    public void Create_names_the_mode_in_the_rejection_message(GameMode mode, int playerCount)
    {
        var result = GameSetup.Create(playerCount, mode, new QueuedDiceRoller());

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Contains(mode.ToString(), rejection.Error.Message);
    }

    [Fact]
    public void Create_sets_the_mode_on_the_resulting_state()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));

        Assert.Equal(GameMode.TwoPlayer, ok.State.Mode);
    }

    [Fact]
    public void Create_marks_no_player_as_neutral()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(4, GameMode.Classic, QueuedDiceRoller.ForRollOff(4)));

        Assert.All(ok.State.Players, p => Assert.False(p.IsNeutral));
    }

    /// <summary>
    /// The carve-out for <see cref="Create_marks_no_player_as_neutral"/>:
    /// that test is Classic-4p-only, not parameterized across modes, so it
    /// needs no exemption. <see cref="GameMode.TwoPlayer"/> instead asserts
    /// exactly one neutral among its three parties (roadmap 4.1).
    /// </summary>
    [Fact]
    public void Create_marks_exactly_one_player_as_neutral_in_TwoPlayer_mode()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));

        Assert.Single(ok.State.Players, p => p.IsNeutral);
    }

    [Fact]
    public void Create_deals_all_42_territories_equitably_across_4_players()
    {
        // Capital still goes through the shared upfront random-deal branch
        // (Classic moved to the unclaimed Claim-phase start in 2.1 — see
        // Create_starts_Classic_with_all_territories_unclaimed_... below).
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(4, GameMode.Capital, QueuedDiceRoller.ForRollOff(4)));

        var counts = ok.State.Territories.Values
            .GroupBy(t => t.Owner!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(4, counts.Count);
        Assert.Equal(42, counts.Values.Sum());
        Assert.All(counts.Values, count => Assert.InRange(count, 10, 11));
    }

    [Fact]
    public void Create_starts_Classic_with_all_territories_unclaimed_full_troop_pools_and_Claim_phase()
    {
        // ForRollOff is tie-free and strictly descending, so player 0 always
        // wins the roll-off — asserted below as the resulting CurrentPlayer.
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(4, GameMode.Classic, QueuedDiceRoller.ForRollOff(4)));

        Assert.Equal(WorldMap.Territories.Count, ok.State.Territories.Count);
        Assert.All(ok.State.Territories.Values, t => Assert.Null(t.Owner));
        Assert.All(ok.State.Territories.Values, t => Assert.Equal(0, t.Troops));
        Assert.All(ok.State.Players, p => Assert.Equal(30, p.TroopsRemaining));
        Assert.Equal(TurnPhase.Claim, ok.State.Turn.Phase);
        Assert.Equal(new PlayerId(0), ok.State.Turn.CurrentPlayer);
        Assert.Empty(ok.State.Log.OfType<TerritoriesAssigned>());
    }

    [Fact]
    public void Create_honors_the_dice_roll_off_winner_as_the_first_Claim_player()
    {
        // Player 2 rolls the unique highest (6), so DetermineFirst must pick
        // players[2] — a hardcoded players[0] fallback would fail this.
        var dice = new QueuedDiceRoller().Enqueue(1).Enqueue(2).Enqueue(6).Enqueue(3);

        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(4, GameMode.Classic, dice));

        Assert.Equal(new PlayerId(2), ok.State.Turn.CurrentPlayer);
    }

    [Theory]
    [InlineData(GameMode.Capital, 3, 35)]
    [InlineData(GameMode.Capital, 4, 30)]
    [InlineData(GameMode.Capital, 5, 25)]
    public void Create_assigns_the_official_starting_troop_pool_via_upfront_deal(GameMode mode, int playerCount, int startingTroops)
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(playerCount, mode, QueuedDiceRoller.ForRollOff(playerCount)));

        var totalRemaining = ok.State.Players.Sum(p => p.TroopsRemaining);
        var territoriesPlaced = ok.State.Territories.Count; // 1 troop auto-placed per dealt territory

        Assert.Equal(playerCount * startingTroops, totalRemaining + territoriesPlaced);
    }

    [Theory]
    [InlineData(3, 35)]
    [InlineData(4, 30)]
    [InlineData(5, 25)]
    public void Create_assigns_the_official_starting_troop_pool_undeducted_for_Classic(int playerCount, int startingTroops)
    {
        // Classic deals nothing upfront (Claim phase), so the full pool
        // stays on every player's remaining count — no per-territory
        // deduction like the upfront-deal modes above.
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(playerCount, GameMode.Classic, QueuedDiceRoller.ForRollOff(playerCount)));

        Assert.All(ok.State.Players, p => Assert.Equal(startingTroops, p.TroopsRemaining));
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer, 2, 2)]
    [InlineData(GameMode.Classic, 3, 5)]
    [InlineData(GameMode.SecretMission, 3, 5)]
    [InlineData(GameMode.Capital, 3, 5)]
    public void PlayerCountRange_returns_the_documented_range_per_mode(GameMode mode, int expectedMin, int expectedMax)
    {
        var (min, max) = GameSetup.PlayerCountRange(mode);

        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
    }

    // NOTE (PR3, roadmap 4.1): this test predates the neutral third party
    // (PR1) and PR2/PR3's Setup generalization. It drives a bare
    // Troops:1-per-command loop through Phase A (legal — 1 is always within
    // the 1..2 budget), then a PlaceNeutralTroopsCommand-per-turn loop
    // through Phase B, and finally keeps draining with the same 1-troop
    // pattern past the Setup->Reinforce transition — the same trick
    // GameStateBuilder.CompleteSetup already relies on — so the final
    // assertion of "every pool is 0" reflects a fully drained state rather
    // than stopping mid-Reinforce with the first player's freshly granted
    // pool sitting unplaced.
    [Fact]
    public void Turn_based_placement_ends_only_when_all_players_reach_zero_remaining_troops()
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(2, GameMode.TwoPlayer, QueuedDiceRoller.ForRollOff(2)));
        var state = ok.State;
        var engine = new Risk.Engine.GameEngine(new QueuedDiceRoller());

        // Phase A
        while (state.Players.Where(p => !p.IsNeutral).Any(p => p.TroopsRemaining > 0))
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;

            var result = engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1));
            var accepted = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
            state = accepted.State;
        }

        Assert.Equal(TurnPhase.Setup, state.Turn.Phase);
        Assert.Equal(26, state.Players.Single(p => p.IsNeutral).TroopsRemaining);

        // Phase B
        while (state.Players.Single(p => p.IsNeutral).TroopsRemaining > 0)
        {
            var actor = state.Turn.CurrentPlayer;
            var neutralId = state.Players.Single(p => p.IsNeutral).Id;
            var territory = state.Territories.First(kv => kv.Value.Owner == neutralId).Key;

            var result = engine.Execute(state, new PlaceNeutralTroopsCommand(actor, territory, 1));
            var accepted = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
            state = accepted.State;
        }

        // Drain the newly granted Reinforce pool too, same pattern as above.
        while (state.Players.Where(p => !p.IsNeutral).Any(p => p.TroopsRemaining > 0))
        {
            var actor = state.Turn.CurrentPlayer;
            var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;

            var result = engine.Execute(state, new PlaceTroopsCommand(actor, territory, 1));
            var accepted = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(result);
            state = accepted.State;
        }

        Assert.All(state.Players, p => Assert.Equal(0, p.TroopsRemaining));
        Assert.Equal(TurnPhase.Reinforce, state.Turn.Phase);
        Assert.Equal(new PlayerId(0), state.Turn.CurrentPlayer);
    }

    [Theory]
    [InlineData(GameMode.Classic, 3)]
    [InlineData(GameMode.SecretMission, 3)]
    [InlineData(GameMode.Capital, 3)]
    public void Classic_SecretMission_Capital_still_reject_Troops_two_in_Setup(GameMode mode, int playerCount)
    {
        var ok = Assert.IsType<CommandResult<GameState, GameEvent>.Ok>(
            GameSetup.Create(playerCount, mode, QueuedDiceRoller.ForRollOff(playerCount)));
        var state = ok.State;
        var engine = new Risk.Engine.GameEngine(new QueuedDiceRoller());

        if (state.Turn.Phase == TurnPhase.Claim)
        {
            state = GameStateBuilder.CompleteClaimPhase(state, engine);
        }

        var actor = state.Turn.CurrentPlayer;
        var territory = state.Territories.First(kv => kv.Value.Owner == actor).Key;

        var result = engine.Execute(state, new PlaceTroopsCommand(actor, territory, 2));

        var rejection = Assert.IsType<CommandResult<GameState, GameEvent>.Rejected>(result);
        Assert.Equal(GameErrorCode.InvalidTroopCount, rejection.Error.Code);
    }
}
