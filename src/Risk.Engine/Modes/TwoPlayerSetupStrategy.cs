using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.State;

namespace Risk.Engine.Modes;

/// <summary>
/// <see cref="GameMode.TwoPlayer"/>'s <see cref="ISetupStrategy"/>: shuffles
/// all 42 territories (same <see cref="Random.Shared"/>-based mechanism as
/// every other mode's deal — not a real <see cref="Deck"/> shuffle) and
/// splits them into three 14-territory piles round-robin: the two humans in
/// <paramref name="players"/>, plus a third, engine-created neutral army
/// (<c>IsNeutral = true</c>) appended last so it never displaces the human
/// completion target (<c>players[0]</c>) used elsewhere in setup/turn
/// advancement. Every party — including the neutral — gets the same
/// official starting troop pool (<paramref name="startingTroops"/>, 40 for
/// 2-player games); 14 of those are spent on the deal (1 troop per dealt
/// territory), leaving the rest in <see cref="PlayerState.TroopsRemaining"/>
/// for turn-based placement (Phase A/B — out of scope for this strategy,
/// resolved by <c>GameEngine</c>).
/// </summary>
public sealed class TwoPlayerSetupStrategy : ISetupStrategy
{
    public GameState Create(IReadOnlyList<PlayerId> players, int startingTroops)
    {
        var neutral = new PlayerId(players.Count);
        var parties = players.Append(neutral).ToArray();
        var partyCount = parties.Length;

        var shuffledTerritoryIds = WorldMap.Territories
            .Select(t => t.Id)
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();

        var territories = new Dictionary<TerritoryId, TerritoryState>();
        var ownedCounts = new int[partyCount];

        for (var i = 0; i < shuffledTerritoryIds.Length; i++)
        {
            var owner = parties[i % partyCount];
            territories[shuffledTerritoryIds[i]] = new TerritoryState(owner, 1);
            ownedCounts[owner.Value]++;
        }

        var playerStates = parties
            .Select((p, i) => new PlayerState(p, [], false, startingTroops - ownedCounts[i], IsNeutral: p == neutral))
            .ToArray();

        var turn = new TurnState(players[0], TurnPhase.Setup);
        IReadOnlyDictionary<TerritoryId, PlayerId> assignments = territories.ToDictionary(kv => kv.Key, kv => kv.Value.Owner!.Value);
        var events = new List<GameEvent> { new TerritoriesAssigned(assignments) };

        return new GameState(territories, playerStates, turn, Deck.CreateStandard(), events,
            new GameStatus.InProgress(), Mode: GameMode.TwoPlayer);
    }
}
