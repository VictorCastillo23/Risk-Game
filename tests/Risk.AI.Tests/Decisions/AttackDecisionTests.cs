using Risk.AI.Decisions;
using Risk.AI.Tests.Fakes;
using Risk.Domain.Map;
using Risk.Domain.Players;
using Risk.Engine.Commands;
using Risk.Engine.State;

namespace Risk.AI.Tests.Decisions;

public class AttackDecisionTests
{
    private static readonly PlayerId Self = new(0);
    private static readonly PlayerId Enemy = new(1);

    [Fact]
    public void Decide_attacks_with_dice_capped_at_three_for_a_strong_origin()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 6, "Alaska")
            .Owns(Enemy, 1, "Alberta")
            .Phase(TurnPhase.Attack)
            .Build();

        var command = AttackDecision.Decide(view, Self, BotMemory.Empty);

        var attack = Assert.IsType<AttackCommand>(command);
        Assert.Equal(new TerritoryId("Alaska"), attack.From);
        Assert.Equal(new TerritoryId("Alberta"), attack.To);
        Assert.Equal(3, attack.DiceCount);
    }

    [Fact]
    public void Decide_attacks_with_one_die_when_the_origin_has_only_two_troops()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 2, "Alaska")
            .Owns(Enemy, 1, "Alberta")
            .Phase(TurnPhase.Attack)
            .Build();

        var command = AttackDecision.Decide(view, Self, BotMemory.Empty);

        var attack = Assert.IsType<AttackCommand>(command);
        Assert.Equal(1, attack.DiceCount);
    }

    [Fact]
    public void Decide_ends_the_phase_when_no_owned_territory_has_more_than_one_troop_adjacent_to_an_enemy()
    {
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 1, "Alaska")
            .Owns(Enemy, 3, "Alberta")
            .Phase(TurnPhase.Attack)
            .Build();

        var command = AttackDecision.Decide(view, Self, BotMemory.Empty);

        Assert.IsType<EndPhaseCommand>(command);
    }

    [Fact]
    public void Decide_ends_the_phase_when_every_legal_attack_has_a_non_positive_score()
    {
        // 2v2: attacker rolls 1 die, defender rolls 2 -> EV strongly negative for the attacker,
        // and there is no mission/capital objective value to override it.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 2, "Alaska")
            .Owns(Enemy, 2, "Alberta")
            .Phase(TurnPhase.Attack)
            .Build();

        var command = AttackDecision.Decide(view, Self, BotMemory.Empty);

        Assert.IsType<EndPhaseCommand>(command);
    }

    [Fact]
    public void Decide_prefers_eliminating_an_enemys_last_territory_over_an_equally_favorable_non_eliminating_attack()
    {
        // Alberta and NorthwestTerritory are both Alaska's neighbors and both North America
        // members, so they share identical continent pressure (self owns only Alaska among
        // NA's other members either way) and identical combat odds (each defender has 1
        // troop, vs a 6-troop, 3-dice attacker). The only asymmetry: EnemyA owns ONLY Alberta
        // (eliminating it wins the game for EnemyA's seat), while EnemyB owns
        // NorthwestTerritory plus Greenland (not their last territory).
        var enemyB = new PlayerId(2);

        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 6, "Alaska")
            .Owns(Enemy, 1, "Alberta")
            .Owns(enemyB, 1, "NorthwestTerritory")
            .Owns(enemyB, 1, "Greenland")
            .Phase(TurnPhase.Attack)
            .Build();

        var command = AttackDecision.Decide(view, Self, BotMemory.Empty);

        // Without EliminationBonus, NorthwestTerritory's lower WorldMap index would win the
        // tie; EliminationBonus must flip the pick to Alberta instead.
        var attack = Assert.IsType<AttackCommand>(command);
        Assert.Equal(new TerritoryId("Alberta"), attack.To);
    }

    [Fact]
    public void Decide_prefers_attacking_the_real_opponent_over_the_identified_neutral_when_all_else_is_equal()
    {
        var opponent = new PlayerId(1);
        var neutral = new PlayerId(2);

        // NorthwestTerritory and Alberta are both Alaska's neighbors, both in North America,
        // and each candidate's owner holds exactly that one territory (symmetric elimination
        // bonus, symmetric continent pressure, symmetric combat odds). The only asymmetry once
        // the neutral is identified is BotWeights.NeutralTargetPenalty.
        var view = PlayerViewBuilder.For(Self)
            .Owns(Self, 6, "Alaska")
            .Owns(neutral, 1, "NorthwestTerritory")
            .Owns(opponent, 1, "Alberta")
            .OtherHandCount(opponent, 3)
            .OtherHandCount(neutral, 0)
            .Phase(TurnPhase.Attack)
            .Build();

        var withoutNeutralIdentified = AttackDecision.Decide(view, Self, BotMemory.Empty);
        var withNeutralIdentified = AttackDecision.Decide(
            view, Self, BotMemory.Empty.WithSeenActor(Self).WithSeenActor(opponent));

        // BotMemory.Empty has no seen actors, so FindTwoPlayerNeutral returns null (more than
        // one party unseen) and no penalty applies: the lower WorldMap index wins the tie.
        var attackWithout = Assert.IsType<AttackCommand>(withoutNeutralIdentified);
        Assert.Equal(new TerritoryId("NorthwestTerritory"), attackWithout.To);

        // With {Self, opponent} seen, the neutral is correctly identified as the sole unseen
        // party, and NeutralTargetPenalty flips the pick to the real opponent's territory.
        var attackWith = Assert.IsType<AttackCommand>(withNeutralIdentified);
        Assert.Equal(new TerritoryId("Alberta"), attackWith.To);
    }
}
