using Risk.Domain.Cards;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;

namespace Risk.Tests.Engine;

public class GameCommandTests
{
    [Fact]
    public void PlaceTroopsCommand_carries_actor_territory_and_troop_count()
    {
        GameCommand command = new PlaceTroopsCommand(new PlayerId(0), new TerritoryId("Alaska"), 3);

        Assert.Equal(new PlayerId(0), command.Actor);
        var place = Assert.IsType<PlaceTroopsCommand>(command);
        Assert.Equal(new TerritoryId("Alaska"), place.Territory);
        Assert.Equal(3, place.Troops);
    }

    [Fact]
    public void AttackCommand_carries_actor_from_to_and_dice_count()
    {
        GameCommand command = new AttackCommand(new PlayerId(1), new TerritoryId("Alaska"), new TerritoryId("Kamchatka"), 2);

        var attack = Assert.IsType<AttackCommand>(command);
        Assert.Equal(new PlayerId(1), attack.Actor);
        Assert.Equal(new TerritoryId("Alaska"), attack.From);
        Assert.Equal(new TerritoryId("Kamchatka"), attack.To);
        Assert.Equal(2, attack.DiceCount);
    }

    [Fact]
    public void FortifyCommand_carries_actor_from_to_and_troops()
    {
        var fortify = new FortifyCommand(new PlayerId(0), new TerritoryId("Alaska"), new TerritoryId("Alberta"), 1);

        Assert.Equal(new TerritoryId("Alaska"), fortify.From);
        Assert.Equal(new TerritoryId("Alberta"), fortify.To);
        Assert.Equal(1, fortify.Troops);
    }

    [Fact]
    public void OccupyCommand_carries_actor_and_troops()
    {
        var occupy = new OccupyCommand(new PlayerId(0), 2);

        Assert.Equal(new PlayerId(0), occupy.Actor);
        Assert.Equal(2, occupy.Troops);
    }

    [Fact]
    public void TradeCardsCommand_carries_actor_and_cards()
    {
        IReadOnlyList<Card> cards = [new WildCard(), new WildCard()];
        var trade = new TradeCardsCommand(new PlayerId(0), cards);

        Assert.Equal(2, trade.Cards.Count);
    }

    [Fact]
    public void EndPhaseCommand_carries_actor()
    {
        var end = new EndPhaseCommand(new PlayerId(0));

        Assert.Equal(new PlayerId(0), end.Actor);
    }
}
