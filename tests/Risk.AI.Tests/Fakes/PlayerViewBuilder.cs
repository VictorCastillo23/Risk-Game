using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Missions;
using Risk.Domain.Players;
using Risk.Engine.State;
using Risk.Engine.Views;

namespace Risk.AI.Tests.Fakes;

/// <summary>
/// Hand-builds a <see cref="PlayerView"/> with no engine involved, so
/// scoring/decision tests are isolated from setup mechanics and board
/// reachability. All 42 <see cref="WorldMap"/> territories are always
/// present in the built <see cref="PlayerView.Territories"/>; any territory
/// never assigned via <see cref="Owns"/>/<see cref="OwnsContinent"/> defaults
/// to unclaimed (<c>Owner: null, Troops: 0</c>) — the same state
/// <see cref="Unclaimed"/> produces explicitly.
/// </summary>
internal sealed class PlayerViewBuilder
{
    private readonly Dictionary<TerritoryId, TerritoryState> _territories;
    private IReadOnlyList<Card> _hand = [];
    private readonly Dictionary<PlayerId, int> _otherHandCounts = [];
    private readonly Dictionary<PlayerId, TerritoryId> _revealedHeadquarters = [];
    private TurnPhase _phase = TurnPhase.Reinforce;
    private PlayerId _currentPlayer;
    private PendingOccupation? _pendingOccupation;
    private bool _mandatoryTradeDown;
    private bool _fortifyUsed;
    private TerritoryId? _ownHeadquarters;
    private MissionCard? _mission;

    private PlayerViewBuilder(PlayerId viewer)
    {
        _currentPlayer = viewer;
        _territories = WorldMap.Territories.ToDictionary(t => t.Id, _ => new TerritoryState(null, 0));
    }

    public static PlayerViewBuilder For(PlayerId viewer) => new(viewer);

    public PlayerViewBuilder Owns(PlayerId owner, int troops, params string[] territoryNames)
    {
        foreach (var name in territoryNames)
        {
            _territories[new TerritoryId(name)] = new TerritoryState(owner, troops);
        }

        return this;
    }

    public PlayerViewBuilder OwnsContinent(PlayerId owner, string continentId, int troops)
    {
        var id = new ContinentId(continentId);

        foreach (var territory in WorldMap.Territories.Where(t => t.ContinentId == id))
        {
            _territories[territory.Id] = new TerritoryState(owner, troops);
        }

        return this;
    }

    public PlayerViewBuilder Unclaimed(params string[] territoryNames)
    {
        foreach (var name in territoryNames)
        {
            _territories[new TerritoryId(name)] = new TerritoryState(null, 0);
        }

        return this;
    }

    public PlayerViewBuilder Hand(params Card[] cards)
    {
        _hand = cards;
        return this;
    }

    public PlayerViewBuilder OtherHandCount(PlayerId other, int count)
    {
        _otherHandCounts[other] = count;
        return this;
    }

    public PlayerViewBuilder Phase(TurnPhase phase)
    {
        _phase = phase;
        return this;
    }

    public PlayerViewBuilder CurrentPlayer(PlayerId player)
    {
        _currentPlayer = player;
        return this;
    }

    public PlayerViewBuilder PendingOccupation(string from, string conquered, int minimumTroops)
    {
        _pendingOccupation = new PendingOccupation(new TerritoryId(from), new TerritoryId(conquered), minimumTroops);
        return this;
    }

    public PlayerViewBuilder MandatoryTradeDown()
    {
        _mandatoryTradeDown = true;
        return this;
    }

    public PlayerViewBuilder FortifyUsed()
    {
        _fortifyUsed = true;
        return this;
    }

    public PlayerViewBuilder OwnHeadquarters(string territoryName)
    {
        _ownHeadquarters = new TerritoryId(territoryName);
        return this;
    }

    public PlayerViewBuilder RevealedHeadquarters(params (PlayerId Player, string Territory)[] headquarters)
    {
        foreach (var (player, territory) in headquarters)
        {
            _revealedHeadquarters[player] = new TerritoryId(territory);
        }

        return this;
    }

    public PlayerViewBuilder Mission(MissionCard mission)
    {
        _mission = mission;
        return this;
    }

    public PlayerView Build() =>
        new(
            _territories,
            _hand,
            _otherHandCounts,
            new TurnState(
                _currentPlayer,
                _phase,
                FortifyUsed: _fortifyUsed,
                PendingOccupation: _pendingOccupation,
                MandatoryTradeDown: _mandatoryTradeDown),
            _ownHeadquarters,
            _revealedHeadquarters,
            _mission);
}
