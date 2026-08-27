using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Events;
using Risk.Engine.State;
using Risk.Web.Services;

namespace Risk.Web.Tests.Services;

public class GameEventPresenterTests
{
    private static readonly PlayerId PlayerOne = new(0);
    private static readonly PlayerId PlayerTwo = new(1);
    private static readonly TerritoryId Alaska = new("Alaska");
    private static readonly TerritoryId Alberta = new("Alberta");

    [Fact]
    public void Describe_TerritoriesAssigned_MentionsTheDeal()
    {
        var e = new TerritoriesAssigned(new Dictionary<TerritoryId, PlayerId> { [Alaska] = PlayerOne });

        var described = GameEventPresenter.Describe(e);

        Assert.False(string.IsNullOrWhiteSpace(described));
        Assert.Contains("territorios", described, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_TroopsPlaced_MentionsPlayerTerritoryAndCount()
    {
        var e = new TroopsPlaced(PlayerOne, Alaska, 3);

        var described = GameEventPresenter.Describe(e);

        Assert.Contains("3", described);
        Assert.Contains("Alaska", described);
    }

    [Fact]
    public void Describe_CardsTraded_MentionsCountAndBonusWithoutNamingCards()
    {
        var cards = new Card[] { new TerritoryCard(Alaska, CardSymbol.Infantry), new WildCard() };
        var e = new CardsTraded(PlayerOne, cards, 6);

        var described = GameEventPresenter.Describe(e);

        Assert.Contains("2", described);
        Assert.Contains("6", described);
    }

    [Fact]
    public void Describe_BattleResolved_MentionsBothTerritoriesAndLosses()
    {
        var e = new BattleResolved(PlayerOne, Alaska, Alberta, [6, 4], [3], 0, 1);

        var described = GameEventPresenter.Describe(e);

        Assert.Contains("Alaska", described);
        Assert.Contains("Alberta", described);
        Assert.Contains("1", described);
    }

    [Fact]
    public void Describe_TerritoryConquered_MentionsConquerorAndTerritory()
    {
        var e = new TerritoryConquered(PlayerOne, PlayerTwo, Alberta);

        var described = GameEventPresenter.Describe(e);

        Assert.Contains("Alberta", described);
    }

    [Fact]
    public void Describe_TerritoryOccupied_MentionsTerritoryAndTroops()
    {
        var e = new TerritoryOccupied(PlayerOne, Alberta, 2);

        var described = GameEventPresenter.Describe(e);

        Assert.Contains("Alberta", described);
        Assert.Contains("2", described);
    }

    [Fact]
    public void Describe_TroopsFortified_MentionsBothTerritoriesAndTroops()
    {
        var e = new TroopsFortified(PlayerOne, Alaska, Alberta, 4);

        var described = GameEventPresenter.Describe(e);

        Assert.Contains("Alaska", described);
        Assert.Contains("Alberta", described);
        Assert.Contains("4", described);
    }

    [Fact]
    public void Describe_PlayerEliminated_MentionsBothPlayers()
    {
        var e = new PlayerEliminated(PlayerTwo, PlayerOne, 3);

        var described = GameEventPresenter.Describe(e);

        Assert.False(string.IsNullOrWhiteSpace(described));
        Assert.Contains("3", described);
    }

    [Fact]
    public void Describe_CardDrawn_MentionsThePlayerButNotWhichCardWasDrawn()
    {
        var e = new CardDrawn(PlayerOne, new TerritoryCard(Alaska, CardSymbol.Infantry));

        var described = GameEventPresenter.Describe(e);

        Assert.False(string.IsNullOrWhiteSpace(described));
        Assert.DoesNotContain("Alaska", described);
        Assert.DoesNotContain("Infantería", described);
    }

    [Fact]
    public void Describe_PhaseChanged_MentionsTheNewPhase()
    {
        var e = new PhaseChanged(TurnPhase.Reinforce, TurnPhase.Attack, PlayerOne);

        var described = GameEventPresenter.Describe(e);

        Assert.Contains("Ataque", described);
    }

    [Fact]
    public void Describe_GameWon_MentionsVictory()
    {
        var e = new GameWon(PlayerOne);

        var described = GameEventPresenter.Describe(e);

        Assert.False(string.IsNullOrWhiteSpace(described));
    }

    [Fact]
    public void Describe_AllElevenEventTypes_ProduceNonEmptyDistinctMessages()
    {
        var events = new GameEvent[]
        {
            new TerritoriesAssigned(new Dictionary<TerritoryId, PlayerId> { [Alaska] = PlayerOne }),
            new TroopsPlaced(PlayerOne, Alaska, 3),
            new CardsTraded(PlayerOne, [new TerritoryCard(Alaska, CardSymbol.Infantry)], 4),
            new BattleResolved(PlayerOne, Alaska, Alberta, [6, 4], [3], 0, 1),
            new TerritoryConquered(PlayerOne, PlayerTwo, Alberta),
            new TerritoryOccupied(PlayerOne, Alberta, 2),
            new TroopsFortified(PlayerOne, Alaska, Alberta, 4),
            new PlayerEliminated(PlayerTwo, PlayerOne, 3),
            new CardDrawn(PlayerOne, new TerritoryCard(Alaska, CardSymbol.Infantry)),
            new PhaseChanged(TurnPhase.Reinforce, TurnPhase.Attack, PlayerOne),
            new GameWon(PlayerOne),
        };

        var messages = events.Select(GameEventPresenter.Describe).ToList();

        Assert.Equal(11, messages.Count);
        Assert.All(messages, m => Assert.False(string.IsNullOrWhiteSpace(m)));
        Assert.Equal(11, messages.Distinct().Count());
    }
}
