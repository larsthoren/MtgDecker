using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ShroudTests
{
    [Fact]
    public void SterlingGrove_Grants_Shroud_To_Other_Enchantments()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var grove = GameCard.Create("Sterling Grove");
        p1.Battlefield.Add(grove);

        var enchantment = new GameCard { Name = "Some Enchantment", CardTypes = CardType.Enchantment };
        p1.Battlefield.Add(enchantment);

        engine.RecalculateState();

        // Other enchantment should have shroud
        enchantment.ActiveKeywords.Should().Contain(Keyword.Shroud);
        // Sterling Grove itself should NOT have shroud (ExcludeSelf)
        grove.ActiveKeywords.Should().NotContain(Keyword.Shroud);
    }

    [Fact]
    public void SterlingGrove_Does_Not_Grant_Shroud_To_Opponent_Enchantments()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var grove = GameCard.Create("Sterling Grove");
        p1.Battlefield.Add(grove);

        var opponentEnch = new GameCard { Name = "Opponent Enchantment", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(opponentEnch);

        engine.RecalculateState();

        // Only P1's enchantments get shroud, not opponent's
        opponentEnch.ActiveKeywords.Should().NotContain(Keyword.Shroud);
    }

    [Fact]
    public void SterlingGrove_Does_Not_Grant_Shroud_To_Non_Enchantments()
    {
        var p1 = new Player(Guid.NewGuid(), "P1", new TestDecisionHandler());
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var grove = GameCard.Create("Sterling Grove");
        p1.Battlefield.Add(grove);

        var creature = new GameCard { Name = "Some Creature", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(creature);

        engine.RecalculateState();

        creature.ActiveKeywords.Should().NotContain(Keyword.Shroud);
    }

    [Fact]
    public async Task Shroud_Prevents_Targeting_By_Activated_Abilities()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has Sterling Grove + another enchantment with shroud
        var grove = GameCard.Create("Sterling Grove");
        p2.Battlefield.Add(grove);
        var enchantment = new GameCard { Name = "Protected", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);
        engine.RecalculateState();

        // Verify shroud
        enchantment.ActiveKeywords.Should().Contain(Keyword.Shroud);

        // P1 has Seal of Cleansing to activate targeting the protected enchantment
        var seal = GameCard.Create("Seal of Cleansing");
        p1.Battlefield.Add(seal);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // Try to activate Seal targeting the protected enchantment
        await engine.ExecuteAction(
            GameAction.ActivateAbility(p1.Id, seal.Id, targetId: enchantment.Id), default);

        // Protected enchantment should still be on the battlefield (shroud prevented targeting)
        p2.Battlefield.Cards.Should().Contain(c => c.Id == enchantment.Id);
        // Log should mention shroud
        state.GameLog.Should().Contain(l => l.Contains("shroud"));
    }

    [Fact]
    public async Task Shroud_Does_Not_Prevent_Targeting_Non_Shroud_Permanents()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has Sterling Grove (no shroud on itself)
        var grove = GameCard.Create("Sterling Grove");
        p2.Battlefield.Add(grove);
        engine.RecalculateState();

        // Grove itself does NOT have shroud
        grove.ActiveKeywords.Should().NotContain(Keyword.Shroud);

        // P1 should be able to target the grove with Naturalize via CastSpell
        var naturalize = GameCard.Create("Naturalize");
        p1.Hand.Add(naturalize);
        p1.ManaPool.Add(ManaColor.Green);
        p1.ManaPool.Add(ManaColor.Colorless);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        handler1.EnqueueTarget(new TargetInfo(grove.Id, p2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, naturalize.Id), default);

        // Naturalize should be on the stack targeting grove
        state.Stack.Should().HaveCount(1);
    }

    [Fact]
    public async Task CastSpell_Excludes_Shroud_From_Eligible_Targets()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has Sterling Grove + another enchantment (protected by shroud)
        var grove = GameCard.Create("Sterling Grove");
        p2.Battlefield.Add(grove);
        var enchA = new GameCard { Name = "Protected A", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchA);
        engine.RecalculateState();

        // Protected A has shroud, grove does not
        enchA.ActiveKeywords.Should().Contain(Keyword.Shroud);
        grove.ActiveKeywords.Should().NotContain(Keyword.Shroud);

        // P1 casts Naturalize - only grove is a legal target (no shroud)
        var naturalize = GameCard.Create("Naturalize");
        p1.Hand.Add(naturalize);
        p1.ManaPool.Add(ManaColor.Green);
        p1.ManaPool.Add(ManaColor.Colorless);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // TestDecisionHandler default picks the first eligible target — should be grove
        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, naturalize.Id), default);

        // Should have been cast successfully
        state.Stack.Should().HaveCount(1);
    }

    [Fact]
    public async Task CastSpell_Fails_When_All_Targets_Have_Shroud()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // Use Swords to Plowshares targeting a creature with shroud
        var creature = new GameCard { Name = "Shrouded Creature", CardTypes = CardType.Creature, BasePower = 3, BaseToughness = 3 };
        creature.ActiveKeywords.Add(Keyword.Shroud);
        p2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        p1.Hand.Add(swords);
        p1.ManaPool.Add(ManaColor.White);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, swords.Id), default);

        // No legal targets, spell should not be cast
        state.Stack.Should().BeEmpty();
        // Card should still be in hand
        p1.Hand.Cards.Should().Contain(c => c.Id == swords.Id);
        state.GameLog.Should().Contain(l => l.Contains("No legal targets"));
    }
}
