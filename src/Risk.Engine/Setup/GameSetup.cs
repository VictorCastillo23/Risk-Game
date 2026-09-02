using Risk.Domain.Cards;
using Risk.Domain.Dice;
using Risk.Domain.Errors;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.Modes;
using Risk.Engine.Results;
using Risk.Engine.State;

namespace Risk.Engine.Setup;

/// <summary>
/// Builds the very first <see cref="GameState"/> for a new game: validates
/// the player count, deals all 42 territories randomly and as equitably as
/// possible, and gives every player their official starting troop pool
/// (placed turn-by-turn afterwards via <c>PlaceTroopsCommand</c>).
/// </summary>
public static class GameSetup
{
    /// <summary>
    /// <see cref="GameMode.SecretMission"/>'s <see cref="ISetupStrategy"/>.
    /// Static, not constructor-injected, per design D3 — a full mode
    /// resolver is deferred until a second mode is wired.
    /// </summary>
    private static readonly ISetupStrategy SecretMissionSetup = new SecretMissionSetupStrategy();

    /// <summary>
    /// <see cref="GameMode.TwoPlayer"/>'s <see cref="ISetupStrategy"/>. Same
    /// static-instance convention as <see cref="SecretMissionSetup"/> (design
    /// D3 — no mode resolver until a third strategy needs one).
    /// </summary>
    private static readonly ISetupStrategy TwoPlayerSetup = new TwoPlayerSetupStrategy();

    private static readonly IReadOnlyDictionary<int, int> StartingTroopsByPlayerCount = new Dictionary<int, int>
    {
        [2] = 40,
        [3] = 35,
        [4] = 30,
        [5] = 25
    };

    /// <summary>
    /// Every mode's legal player-count range. <c>TwoPlayer</c> is exactly 2
    /// (its neutral third army is not a real player); every other mode is
    /// 3-5. No mode accepts 6. Deliberately has no <c>_</c> discard arm: an
    /// unhandled <see cref="GameMode"/> value throws <see cref="SwitchExpressionException"/>
    /// (a programmer error, per this repo's convention) instead of silently
    /// returning a wrong range, and adding a 5th mode without updating this
    /// switch produces a CS8524 exhaustiveness warning at compile time.
    /// </summary>
    public static (int Min, int Max) PlayerCountRange(GameMode mode) => mode switch
    {
        GameMode.TwoPlayer => (2, 2),
        GameMode.Classic or GameMode.SecretMission or GameMode.Capital => (3, 5)
    };

    /// <param name="playerCount">How many players are seated.</param>
    /// <param name="mode">Which mode's setup rules apply.</param>
    /// <param name="dice">
    /// The dice source, required (no ambient <see cref="Random"/>) per this
    /// repo's convention. Only <see cref="GameMode.Classic"/> actually rolls
    /// it (via <c>TurnOrder.DetermineFirst</c>, to pick the Claim-phase
    /// starting player); every other mode ignores it.
    /// </param>
    public static CommandResult<GameState, GameEvent> Create(int playerCount, GameMode mode, IDiceRoller dice)
    {
        var (min, max) = PlayerCountRange(mode);

        if (playerCount < min || playerCount > max)
        {
            var message = min == max
                ? $"{mode} mode requires exactly {min} players."
                : $"{mode} mode requires between {min} and {max} players.";

            return new CommandResult<GameState, GameEvent>.Rejected(
                new GameError(GameErrorCode.InvalidPlayerCount, message));
        }

        var players = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToArray();
        var startingTroops = StartingTroopsByPlayerCount[playerCount];

        if (mode == GameMode.SecretMission)
        {
            var s = SecretMissionSetup.Create(players, startingTroops);
            return new CommandResult<GameState, GameEvent>.Ok(s, s.Log);
        }

        if (mode == GameMode.TwoPlayer)
        {
            var s = TwoPlayerSetup.Create(players, startingTroops);
            return new CommandResult<GameState, GameEvent>.Ok(s, s.Log);
        }

        if (mode is GameMode.Classic or GameMode.Capital)
        {
            // Classic and Capital both start with every territory unclaimed
            // (Claim phase) — no upfront deal, so nobody's pool is deducted
            // yet. The dice roll-off (TurnOrder.DetermineFirst) picks who
            // claims/places first, matching the classic rulebook's opening
            // ritual. Capital reuses this path unchanged (5.1); it diverges
            // from Classic only after setup placement, when it transitions
            // to TurnPhase.SelectHeadquarters instead of Reinforce.
            var unclaimedTerritories = WorldMap.Territories
                .ToDictionary(t => t.Id, _ => new TerritoryState(Owner: null, Troops: 0));

            var claimPlayerStates = players
                .Select(p => new PlayerState(p, [], false, startingTroops))
                .ToArray();

            var firstPlayer = TurnOrder.DetermineFirst(players, dice);
            var claimTurn = new TurnState(firstPlayer, TurnPhase.Claim);

            var claimState = new GameState(unclaimedTerritories, claimPlayerStates, claimTurn,
                Deck.CreateStandard(), [], new GameStatus.InProgress(), Mode: mode);

            return new CommandResult<GameState, GameEvent>.Ok(claimState, []);
        }

        // SecretMission, TwoPlayer, Classic, and Capital are GameMode's only
        // values (State/GameMode.cs) and are all handled above — this point
        // is reachable only if a 5th mode is added here without updating
        // this method, per this repo's convention of throwing on an
        // unhandled case rather than silently falling through.
        throw new InvalidOperationException($"Unreachable: unhandled {nameof(GameMode)} value {mode}.");
    }
}
