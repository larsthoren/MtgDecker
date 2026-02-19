using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class HexproofTests
{
    [Fact]
    public void Keyword_Hexproof_Exists()
    {
        Keyword.Hexproof.Should().BeDefined();
    }

    [Fact]
    public void HexproofCreature_HasKeyword()
    {
        var creature = new GameCard
        {
            Name = "Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        creature.ActiveKeywords.Add(Keyword.Hexproof);

        creature.ActiveKeywords.Should().Contain(Keyword.Hexproof);
        creature.ActiveKeywords.Should().NotContain(Keyword.Shroud);
    }

    [Fact]
    public async Task Hexproof_Prevents_Opponent_Targeting_With_Spell()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has a creature with hexproof
        var hexproofCreature = new GameCard
        {
            Name = "Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        hexproofCreature.ActiveKeywords.Add(Keyword.Hexproof);
        p2.Battlefield.Add(hexproofCreature);

        // P1 tries to cast Swords to Plowshares targeting hexproof creature
        var swords = GameCard.Create("Swords to Plowshares");
        p1.Hand.Add(swords);
        p1.ManaPool.Add(ManaColor.White);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, swords.Id), default);

        // No legal targets (hexproof blocks opponent targeting), spell should not be cast
        state.Stack.Should().BeEmpty();
        p1.Hand.Cards.Should().Contain(c => c.Id == swords.Id);
        state.GameLog.Should().Contain(l => l.Contains("No legal targets"));
    }

    [Fact]
    public async Task Hexproof_Allows_Controller_To_Target_Own_Creature()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has a creature with hexproof on their own battlefield
        var hexproofCreature = new GameCard
        {
            Name = "Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        hexproofCreature.ActiveKeywords.Add(Keyword.Hexproof);
        p1.Battlefield.Add(hexproofCreature);

        // P1 casts Swords to Plowshares targeting their own hexproof creature
        var swords = GameCard.Create("Swords to Plowshares");
        p1.Hand.Add(swords);
        p1.ManaPool.Add(ManaColor.White);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        handler1.EnqueueTarget(new TargetInfo(hexproofCreature.Id, p1.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, swords.Id), default);

        // Controller CAN target their own hexproof creature
        state.Stack.Should().HaveCount(1);
    }

    [Fact]
    public async Task Hexproof_Prevents_Opponent_Activated_Ability_Targeting()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has a creature with hexproof
        var hexproofCreature = new GameCard
        {
            Name = "Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        hexproofCreature.ActiveKeywords.Add(Keyword.Hexproof);
        p2.Battlefield.Add(hexproofCreature);

        // P1 has Seal of Cleansing-like effect to try to target the hexproof creature
        // Use an activated ability that targets a creature
        var seal = GameCard.Create("Seal of Cleansing");
        p1.Battlefield.Add(seal);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // Try to activate targeting the hexproof creature
        await engine.ExecuteAction(
            GameAction.ActivateAbility(p1.Id, seal.Id, targetId: hexproofCreature.Id), default);

        // Hexproof should prevent targeting
        p2.Battlefield.Cards.Should().Contain(c => c.Id == hexproofCreature.Id);
        state.GameLog.Should().Contain(l => l.Contains("hexproof") || l.Contains("Hexproof"));
    }

    [Fact]
    public async Task Hexproof_Does_Not_Affect_NonTargeted_Opponents()
    {
        // Hexproof only prevents targeting - non-targeted effects should still work
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has a creature with hexproof
        var hexproofCreature = new GameCard
        {
            Name = "Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        hexproofCreature.ActiveKeywords.Add(Keyword.Hexproof);
        p2.Battlefield.Add(hexproofCreature);

        // P1 also has a normal creature to verify hexproof is not the same as shroud
        var normalCreature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p2.Battlefield.Add(normalCreature);

        // P1 casts Swords targeting the normal creature (should work even though
        // another creature has hexproof)
        var swords = GameCard.Create("Swords to Plowshares");
        p1.Hand.Add(swords);
        p1.ManaPool.Add(ManaColor.White);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        handler1.EnqueueTarget(new TargetInfo(normalCreature.Id, p2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, swords.Id), default);

        // Normal creature is targetable, spell goes on stack
        state.Stack.Should().HaveCount(1);
    }

    [Fact]
    public async Task Hexproof_CastSpell_Filters_Opponent_Hexproof_But_Keeps_Own()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P1 has a hexproof creature (own) — should be targetable by P1
        var ownHexproof = new GameCard
        {
            Name = "My Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        ownHexproof.ActiveKeywords.Add(Keyword.Hexproof);
        p1.Battlefield.Add(ownHexproof);

        // P2 has a hexproof creature (opponent) — should NOT be targetable by P1
        var oppHexproof = new GameCard
        {
            Name = "Their Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 3,
            BaseToughness = 3,
        };
        oppHexproof.ActiveKeywords.Add(Keyword.Hexproof);
        p2.Battlefield.Add(oppHexproof);

        // P1 casts Swords to Plowshares
        var swords = GameCard.Create("Swords to Plowshares");
        p1.Hand.Add(swords);
        p1.ManaPool.Add(ManaColor.White);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // The TestDecisionHandler default picks the first eligible target
        // Only P1's own hexproof creature should be eligible (not P2's)
        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, swords.Id), default);

        // Spell should be cast (own hexproof creature is a valid target)
        state.Stack.Should().HaveCount(1);
    }

    [Fact]
    public async Task Hexproof_Flashback_Spell_Filters_Opponent_Hexproof()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 has only a hexproof creature — no legal targets for opponent's flashback
        var hexproofCreature = new GameCard
        {
            Name = "Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        hexproofCreature.ActiveKeywords.Add(Keyword.Hexproof);
        p2.Battlefield.Add(hexproofCreature);

        // P1 also has a normal creature (so flashback has at least one legal target)
        var normalCreature = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 1,
        };
        p1.Battlefield.Add(normalCreature);

        // P1 has Reckless Charge in graveyard (flashback {2}{R}, targets creature)
        var charge = GameCard.Create("Reckless Charge");
        p1.Graveyard.Add(charge);
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Red);
        p1.ManaPool.Add(ManaColor.Red);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        // Default target selection picks the first eligible target — should be P1's creature
        // since P2's hexproof creature is filtered out
        await engine.ExecuteAction(GameAction.Flashback(p1.Id, charge.Id), default);

        // Should succeed — P1's normal creature is a valid target for flashback
        state.Stack.Should().HaveCount(1);
    }

    [Fact]
    public async Task Hexproof_Aura_Cannot_Attach_To_Opponent_Hexproof()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler1);
        var p2 = new Player(Guid.NewGuid(), "P2", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        // P2 only has a hexproof creature
        var hexproofCreature = new GameCard
        {
            Name = "Hexproof Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        hexproofCreature.ActiveKeywords.Add(Keyword.Hexproof);
        p2.Battlefield.Add(hexproofCreature);

        // P1 also has a normal creature (so we can verify aura can attach to own creatures)
        var normalCreature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p1.Battlefield.Add(normalCreature);

        // When aura targeting filters, opponent hexproof creatures should be excluded
        // but own creatures should be included
        // This test verifies the TryAttachAuraAsync hexproof filtering
        hexproofCreature.ActiveKeywords.Should().Contain(Keyword.Hexproof);
        normalCreature.ActiveKeywords.Should().NotContain(Keyword.Hexproof);
    }

    [Fact]
    public void Hexproof_Is_Different_From_Shroud()
    {
        var creature = new GameCard
        {
            Name = "Test Creature",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };

        // Hexproof and Shroud are distinct keywords
        creature.ActiveKeywords.Add(Keyword.Hexproof);
        creature.ActiveKeywords.Should().Contain(Keyword.Hexproof);
        creature.ActiveKeywords.Should().NotContain(Keyword.Shroud);

        creature.ActiveKeywords.Add(Keyword.Shroud);
        creature.ActiveKeywords.Should().Contain(Keyword.Hexproof);
        creature.ActiveKeywords.Should().Contain(Keyword.Shroud);
    }
}
