using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class OracleFixesTests
{
    private static (GameState state, Player p1, Player p2) CreateGameState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", h1);
        var p2 = new Player(Guid.NewGuid(), "Bob", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2);
    }

    private static (GameState state, GameEngine engine, TestDecisionHandler h1, TestDecisionHandler h2)
        CreateEngineState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (state, engine, h1, h2);
    }

    // ═══════════════════════════════════════════════════════════════════
    // FIX 1: REB / BEB / Hydroblast — permanent-destruction mode
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RedElementalBlast_CanDestroyBluePermanent()
    {
        var (state, p1, p2) = CreateGameState();

        var blueCreature = new GameCard
        {
            Name = "Delver of Secrets",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{U}"),
        };
        p2.Battlefield.Add(blueCreature);

        var rebCard = new GameCard { Name = "Red Elemental Blast", CardTypes = CardType.Instant };
        var spell = new StackObject(rebCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(blueCreature.Id, p2.Id, ZoneType.Battlefield) }, 0);

        new PyroblastEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Delver of Secrets");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Delver of Secrets");
    }

    [Fact]
    public void BlueElementalBlast_CanDestroyRedPermanent()
    {
        var (state, p1, p2) = CreateGameState();

        var redCreature = new GameCard
        {
            Name = "Goblin Lackey",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{R}"),
        };
        p2.Battlefield.Add(redCreature);

        var bebCard = new GameCard { Name = "Blue Elemental Blast", CardTypes = CardType.Instant };
        var spell = new StackObject(bebCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(redCreature.Id, p2.Id, ZoneType.Battlefield) }, 0);

        new BlueElementalBlastEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Lackey");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Lackey");
    }

    [Fact]
    public void Hydroblast_CanDestroyRedPermanent()
    {
        var (state, p1, p2) = CreateGameState();

        var redCreature = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{1}{R}"),
        };
        p2.Battlefield.Add(redCreature);

        var hydroblastCard = new GameCard { Name = "Hydroblast", CardTypes = CardType.Instant };
        var spell = new StackObject(hydroblastCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(redCreature.Id, p2.Id, ZoneType.Battlefield) }, 0);

        new BlueElementalBlastEffect().Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Piledriver");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Piledriver");
    }

    [Fact]
    public void SpellOrPermanent_TargetFilter_AllowsBothStackAndBattlefield()
    {
        var filter = TargetFilter.SpellOrPermanent();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        var spell = new GameCard { Name = "Bolt", CardTypes = CardType.Instant };

        filter.IsLegal(creature, ZoneType.Battlefield).Should().BeTrue();
        filter.IsLegal(spell, ZoneType.Stack).Should().BeTrue();
        filter.IsLegal(creature, ZoneType.Hand).Should().BeFalse();
    }

    [Fact]
    public void RedElementalBlast_CardDefinition_UsesSpellOrPermanent()
    {
        CardDefinitions.TryGet("Red Elemental Blast", out var def).Should().BeTrue();
        var filter = def!.TargetFilter!;
        filter.IsLegal(new GameCard { Name = "X" }, ZoneType.Battlefield).Should().BeTrue();
        filter.IsLegal(new GameCard { Name = "X" }, ZoneType.Stack).Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // FIX 2: Stifle — proper targeting
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Stifle_WithMultipleTriggeredAbilities_PlayerChoosesWhichToCounter()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var source1 = GameCard.Create("Siege-Gang Commander");
        var source2 = GameCard.Create("Goblin Matron");
        var trigger1 = new TriggeredAbilityStackObject(source1, state.Player2.Id, new DrawCardEffect());
        var trigger2 = new TriggeredAbilityStackObject(source2, state.Player2.Id, new DrawCardEffect());
        state.StackPush(trigger1);
        state.StackPush(trigger2);

        // Player chooses to counter the second trigger (Goblin Matron)
        h1.EnqueueCardChoice(source2.Id);

        var stifleCard = new GameCard { Name = "Stifle", CardTypes = CardType.Instant };
        var spell = new StackObject(stifleCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new StifleEffect();
        await effect.ResolveAsync(state, spell, state.Player1.DecisionHandler);

        // Matron trigger removed, Commander trigger remains
        state.Stack.OfType<TriggeredAbilityStackObject>().Should().HaveCount(1);
        state.Stack.OfType<TriggeredAbilityStackObject>().Single().Source.Name
            .Should().Be("Siege-Gang Commander");
    }

    [Fact]
    public async Task Stifle_WithSingleTriggeredAbility_AutoCounters()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var source = GameCard.Create("Siege-Gang Commander");
        var trigger = new TriggeredAbilityStackObject(source, state.Player2.Id, new DrawCardEffect());
        state.StackPush(trigger);

        var stifleCard = new GameCard { Name = "Stifle", CardTypes = CardType.Instant };
        var spell = new StackObject(stifleCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new StifleEffect();
        await effect.ResolveAsync(state, spell, state.Player1.DecisionHandler);

        state.Stack.OfType<TriggeredAbilityStackObject>().Should().BeEmpty();
        state.GameLog.Should().Contain(l => l.Contains("counters"));
    }

    [Fact]
    public async Task Stifle_WithNoTriggeredAbility_Fizzles()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var stifleCard = new GameCard { Name = "Stifle", CardTypes = CardType.Instant };
        var spell = new StackObject(stifleCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new StifleEffect();
        await effect.ResolveAsync(state, spell, state.Player1.DecisionHandler);

        state.GameLog.Should().Contain(l => l.Contains("fizzles"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FIX 3: Tsabo's Web — lands with non-mana activated abilities don't untap
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TsabosWeb_PreventsUntapOfLandWithActivatedAbility()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var web = GameCard.Create("Tsabo's Web");
        state.Player1.Battlefield.Add(web);
        engine.RecalculateState();

        // Rishadan Port has an activated ability (tap target land) — not a mana ability
        var port = GameCard.Create("Rishadan Port");
        port.IsTapped = true;
        state.Player1.Battlefield.Add(port);
        engine.RecalculateState();

        port.ActiveKeywords.Should().Contain(Keyword.DoesNotUntap);
    }

    [Fact]
    public void TsabosWeb_BasicLandStillUntaps()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var web = GameCard.Create("Tsabo's Web");
        state.Player1.Battlefield.Add(web);

        var mountain = GameCard.Create("Mountain");
        mountain.IsTapped = true;
        state.Player1.Battlefield.Add(mountain);
        engine.RecalculateState();

        mountain.ActiveKeywords.Should().NotContain(Keyword.DoesNotUntap);
    }

    [Fact]
    public void TsabosWeb_FetchlandDoesNotUntap()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var web = GameCard.Create("Tsabo's Web");
        state.Player1.Battlefield.Add(web);

        var fetch = GameCard.Create("Wooded Foothills");
        fetch.IsTapped = true;
        state.Player1.Battlefield.Add(fetch);
        engine.RecalculateState();

        fetch.ActiveKeywords.Should().Contain(Keyword.DoesNotUntap);
    }

    [Fact]
    public void TsabosWeb_WastelandDoesNotUntap()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var web = GameCard.Create("Tsabo's Web");
        state.Player1.Battlefield.Add(web);

        var wasteland = GameCard.Create("Wasteland");
        wasteland.IsTapped = true;
        state.Player1.Battlefield.Add(wasteland);
        engine.RecalculateState();

        wasteland.ActiveKeywords.Should().Contain(Keyword.DoesNotUntap);
    }

    // ═══════════════════════════════════════════════════════════════════
    // FIX 4: Flash of Insight — dynamic flashback X
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FlashOfInsight_FlashbackExiling3BlueCards_LooksAtTop3()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        // Put Flash of Insight in graveyard
        var foi = GameCard.Create("Flash of Insight");
        state.Player1.Graveyard.Add(foi);

        // Put 3 blue cards in graveyard (not the FOI itself)
        var blue1 = new GameCard { Name = "Brainstorm", ManaCost = ManaCost.Parse("{U}"), CardTypes = CardType.Instant };
        var blue2 = new GameCard { Name = "Counterspell", ManaCost = ManaCost.Parse("{U}{U}"), CardTypes = CardType.Instant };
        var blue3 = new GameCard { Name = "Force of Will", ManaCost = ManaCost.Parse("{3}{U}{U}"), CardTypes = CardType.Instant };
        state.Player1.Graveyard.Add(blue1);
        state.Player1.Graveyard.Add(blue2);
        state.Player1.Graveyard.Add(blue3);

        // Library has 5 cards
        var lib1 = new GameCard { Name = "Card A" };
        var lib2 = new GameCard { Name = "Card B" };
        var lib3 = new GameCard { Name = "Card C" };
        var lib4 = new GameCard { Name = "Card D" };
        var lib5 = new GameCard { Name = "Card E" };
        state.Player1.Library.Add(lib5);
        state.Player1.Library.Add(lib4);
        state.Player1.Library.Add(lib3);
        state.Player1.Library.Add(lib2);
        state.Player1.Library.Add(lib1);

        // Add {1}{U} mana for flashback cost
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);
        state.Player1.ManaPool.Add(ManaColor.Blue, 1);

        // Exile choice: exile all 3 blue cards
        h1.EnqueueExileChoice((cards, max) => cards.Take(3).ToList());

        // Card choice for Flash of Insight: pick Card B
        h1.EnqueueCardChoice(lib2.Id);

        // Cast flashback
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;
        state.PriorityPlayer = state.Player1;
        h1.EnqueueAction(GameAction.Flashback(state.Player1.Id, foi.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync(CancellationToken.None);

        // Verify: FOI exiled (flashback), 3 blue cards exiled, X=3
        state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Brainstorm");
        state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Counterspell");
        state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Force of Will");
        state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Flash of Insight");

        // Card B should be in hand (chosen from top 3)
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Card B");

        // The log should mention X=3
        state.GameLog.Should().Contain(l => l.Contains("X=3"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // FIX 5: Gloom — activated ability cost increase for white enchantments
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Gloom_IncreasesActivatedAbilityCostOfWhiteEnchantment()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        // Put Gloom on the battlefield (opponent controls it)
        var gloom = GameCard.Create("Gloom", "Enchantment");
        state.Player2.Battlefield.Add(gloom);
        engine.RecalculateState();

        // Circle of Protection: Red is a {1}{W} white enchantment
        // with activated ability cost {1}
        var cop = GameCard.Create("Circle of Protection: Red");
        state.Player1.Battlefield.Add(cop);

        // With Gloom, the {1} activation cost should become {4} ({1} + {3})
        // Player has only {1} mana — not enough with Gloom's tax
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, cop.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;
        state.PriorityPlayer = state.Player1;
        await engine.RunPriorityAsync(CancellationToken.None);

        // Should fail due to insufficient mana (needs 4, has 1)
        state.GameLog.Should().Contain(l => l.Contains("not enough mana"));
    }

    [Fact]
    public async Task Gloom_DoesNotAffectNonWhiteEnchantmentAbilities()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        // Put Gloom on the battlefield
        var gloom = GameCard.Create("Gloom", "Enchantment");
        state.Player2.Battlefield.Add(gloom);
        engine.RecalculateState();

        // River Boa is a green creature with {G} activated ability (not a white enchantment)
        var boa = GameCard.Create("River Boa");
        state.Player1.Battlefield.Add(boa);
        state.Player1.ManaPool.Add(ManaColor.Green, 1);

        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, boa.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;
        state.PriorityPlayer = state.Player1;
        await engine.RunPriorityAsync(CancellationToken.None);

        // Should succeed — Gloom only affects white enchantments
        state.GameLog.Should().Contain(l => l.Contains("ability is put on the stack"));
    }

    [Fact]
    public async Task Gloom_WhiteEnchantmentCanActivateWithEnoughMana()
    {
        var (state, engine, h1, h2) = CreateEngineState();

        var gloom = GameCard.Create("Gloom", "Enchantment");
        state.Player2.Battlefield.Add(gloom);
        engine.RecalculateState();

        var cop = GameCard.Create("Circle of Protection: Red");
        state.Player1.Battlefield.Add(cop);

        // With Gloom, {1} activation becomes {4}, so provide 4 colorless mana
        state.Player1.ManaPool.Add(ManaColor.Colorless, 4);

        h1.EnqueueAction(GameAction.ActivateAbility(state.Player1.Id, cop.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));

        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;
        state.PriorityPlayer = state.Player1;
        await engine.RunPriorityAsync(CancellationToken.None);

        // Should succeed with enough mana
        state.GameLog.Should().Contain(l => l.Contains("ability is put on the stack"));
    }
}
