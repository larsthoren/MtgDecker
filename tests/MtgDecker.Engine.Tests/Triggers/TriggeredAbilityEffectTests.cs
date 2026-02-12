using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class TriggeredAbilityEffectTests
{
    private (GameState state, Player player1, Player player2, TestDecisionHandler handler1, TestDecisionHandler handler2) CreateSetup()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler1);
        var p2 = new Player(Guid.NewGuid(), "Player 2", handler2);
        var state = new GameState(p1, p2);
        return (state, p1, p2, handler1, handler2);
    }

    // === DrawCardEffect ===

    [Fact]
    public async Task DrawCardEffect_DrawsFromLibrary_AddsToHand()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var libraryCard = new GameCard { Name = "Forest" };
        player.Library.Add(libraryCard);

        var effect = new DrawCardEffect();
        var source = new GameCard { Name = "Argothian Enchantress" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Cards.Should().Contain(c => c.Id == libraryCard.Id);
        player.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task DrawCardEffect_EmptyLibrary_DoesNothing()
    {
        var (state, player, _, handler, _) = CreateSetup();

        var effect = new DrawCardEffect();
        var source = new GameCard { Name = "Argothian Enchantress" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
    }

    [Fact]
    public async Task DrawCardEffect_Logs()
    {
        var (state, player, _, handler, _) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Card" });

        var effect = new DrawCardEffect();
        var source = new GameCard { Name = "Enchantress" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Player 1") && l.Contains("draws a card"));
    }

    // === UntapSelfEffect ===

    [Fact]
    public async Task UntapSelfEffect_UntapsSource()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var source = new GameCard { Name = "Goblin Sharpshooter", IsTapped = true };
        player.Battlefield.Add(source);

        var effect = new UntapSelfEffect();
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        source.IsTapped.Should().BeFalse();
    }

    [Fact]
    public async Task UntapSelfEffect_AlreadyUntapped_StaysUntapped()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var source = new GameCard { Name = "Goblin Sharpshooter", IsTapped = false };

        var effect = new UntapSelfEffect();
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        source.IsTapped.Should().BeFalse();
    }

    [Fact]
    public async Task UntapSelfEffect_Logs()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var source = new GameCard { Name = "Goblin Sharpshooter", IsTapped = true };

        var effect = new UntapSelfEffect();
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Goblin Sharpshooter") && l.Contains("untaps"));
    }

    // === PutCreatureFromHandEffect ===

    [Fact]
    public async Task PutCreatureFromHandEffect_PutsMatchingCreatureOntoBattlefield()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var goblin = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 2,
            Subtypes = ["Goblin"],
        };
        player.Hand.Add(goblin);
        handler.EnqueueCardChoice(goblin.Id);

        var effect = new PutCreatureFromHandEffect("Goblin");
        var source = new GameCard { Name = "Goblin Lackey" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
        player.Hand.Cards.Should().NotContain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task PutCreatureFromHandEffect_NoMatchingCreature_Logs()
    {
        var (state, player, _, handler, _) = CreateSetup();
        // Only a non-goblin in hand
        var elf = new GameCard
        {
            Name = "Llanowar Elves",
            CardTypes = CardType.Creature,
            Subtypes = ["Elf"],
        };
        player.Hand.Add(elf);

        var effect = new PutCreatureFromHandEffect("Goblin");
        var source = new GameCard { Name = "Goblin Lackey" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Battlefield.Count.Should().Be(0);
        state.GameLog.Should().Contain(l => l.Contains("No Goblin creatures"));
    }

    [Fact]
    public async Task PutCreatureFromHandEffect_PlayerDeclinesOptional_NothingHappens()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var goblin = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            Subtypes = ["Goblin"],
        };
        player.Hand.Add(goblin);
        handler.EnqueueCardChoice(null); // Decline

        var effect = new PutCreatureFromHandEffect("Goblin");
        var source = new GameCard { Name = "Goblin Lackey" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Battlefield.Count.Should().Be(0);
        player.Hand.Count.Should().Be(1);
    }

    [Fact]
    public async Task PutCreatureFromHandEffect_SetsTurnEnteredBattlefield()
    {
        var (state, player, _, handler, _) = CreateSetup();
        state.TurnNumber = 5;
        var goblin = new GameCard
        {
            Name = "Goblin Warchief",
            CardTypes = CardType.Creature,
            Subtypes = ["Goblin"],
        };
        player.Hand.Add(goblin);
        handler.EnqueueCardChoice(goblin.Id);

        var effect = new PutCreatureFromHandEffect("Goblin");
        var source = new GameCard { Name = "Goblin Lackey" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        goblin.TurnEnteredBattlefield.Should().Be(5);
    }

    // === PiledriverPumpEffect ===

    [Fact]
    public async Task PiledriverPumpEffect_WithOtherAttackingGoblins_PumpsSource()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var piledriver = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 2,
            Subtypes = ["Goblin"],
        };
        var goblin1 = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        var goblin2 = new GameCard { Name = "Mogg Fanatic", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        player.Battlefield.Add(piledriver);
        player.Battlefield.Add(goblin1);
        player.Battlefield.Add(goblin2);

        // Set up combat with all three attacking
        state.Combat = new CombatState(player.Id, state.Player2.Id);
        state.Combat.DeclareAttacker(piledriver.Id);
        state.Combat.DeclareAttacker(goblin1.Id);
        state.Combat.DeclareAttacker(goblin2.Id);

        var effect = new PiledriverPumpEffect();
        var context = new EffectContext(state, player, piledriver, handler);

        await effect.Execute(context);

        // 2 other goblins attacking -> +4/+0
        state.ActiveEffects.Should().HaveCount(1);
        state.ActiveEffects[0].PowerMod.Should().Be(4);
        state.ActiveEffects[0].ToughnessMod.Should().Be(0);
        state.ActiveEffects[0].UntilEndOfTurn.Should().BeTrue();
    }

    [Fact]
    public async Task PiledriverPumpEffect_NoOtherAttackers_NoPump()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var piledriver = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            BasePower = 1,
            BaseToughness = 2,
            Subtypes = ["Goblin"],
        };
        player.Battlefield.Add(piledriver);

        state.Combat = new CombatState(player.Id, state.Player2.Id);
        state.Combat.DeclareAttacker(piledriver.Id);

        var effect = new PiledriverPumpEffect();
        var context = new EffectContext(state, player, piledriver, handler);

        await effect.Execute(context);

        state.ActiveEffects.Should().BeEmpty();
    }

    [Fact]
    public async Task PiledriverPumpEffect_NoCombat_DoesNothing()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var piledriver = new GameCard { Name = "Goblin Piledriver" };

        var effect = new PiledriverPumpEffect();
        var context = new EffectContext(state, player, piledriver, handler);

        await effect.Execute(context);

        state.ActiveEffects.Should().BeEmpty();
    }

    [Fact]
    public async Task PiledriverPumpEffect_NonGoblinAttacker_NotCounted()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var piledriver = new GameCard
        {
            Name = "Goblin Piledriver",
            CardTypes = CardType.Creature,
            Subtypes = ["Goblin"],
        };
        var elf = new GameCard { Name = "Llanowar Elves", CardTypes = CardType.Creature, Subtypes = ["Elf"] };
        player.Battlefield.Add(piledriver);
        player.Battlefield.Add(elf);

        state.Combat = new CombatState(player.Id, state.Player2.Id);
        state.Combat.DeclareAttacker(piledriver.Id);
        state.Combat.DeclareAttacker(elf.Id);

        var effect = new PiledriverPumpEffect();
        var context = new EffectContext(state, player, piledriver, handler);

        await effect.Execute(context);

        // Elf is not a Goblin, so no pump
        state.ActiveEffects.Should().BeEmpty();
    }

    // === DestroyAllSubtypeEffect ===

    [Fact]
    public async Task DestroyAllSubtypeEffect_DestroysAllOfSubtype_BothPlayers()
    {
        var (state, player1, player2, handler, _) = CreateSetup();
        var goblin1 = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        var goblin2 = new GameCard { Name = "Mogg Fanatic", CardTypes = CardType.Creature, Subtypes = ["Goblin"] };
        var elf = new GameCard { Name = "Llanowar Elves", CardTypes = CardType.Creature, Subtypes = ["Elf"] };
        player1.Battlefield.Add(goblin1);
        player2.Battlefield.Add(goblin2);
        player1.Battlefield.Add(elf);

        var effect = new DestroyAllSubtypeEffect("Goblin");
        var source = new GameCard { Name = "Goblin Pyromancer" };
        var context = new EffectContext(state, player1, source, handler);

        await effect.Execute(context);

        player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Lackey");
        player2.Battlefield.Cards.Should().NotContain(c => c.Name == "Mogg Fanatic");
        player1.Battlefield.Cards.Should().Contain(c => c.Name == "Llanowar Elves");
        player1.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Lackey");
        player2.Graveyard.Cards.Should().Contain(c => c.Name == "Mogg Fanatic");
    }

    [Fact]
    public async Task DestroyAllSubtypeEffect_NoMatchingSubtype_NothingHappens()
    {
        var (state, player1, _, handler, _) = CreateSetup();
        var elf = new GameCard { Name = "Llanowar Elves", CardTypes = CardType.Creature, Subtypes = ["Elf"] };
        player1.Battlefield.Add(elf);

        var effect = new DestroyAllSubtypeEffect("Goblin");
        var source = new GameCard { Name = "Test" };
        var context = new EffectContext(state, player1, source, handler);

        await effect.Execute(context);

        player1.Battlefield.Count.Should().Be(1);
    }

    // === PyromancerEffect ===

    [Fact]
    public async Task PyromancerEffect_AddsGoblinPumpAndDelayedDestroy()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var pyromancer = new GameCard
        {
            Name = "Goblin Pyromancer",
            CardTypes = CardType.Creature,
            Subtypes = ["Goblin"],
        };

        var effect = new PyromancerEffect();
        var context = new EffectContext(state, player, pyromancer, handler);

        await effect.Execute(context);

        state.ActiveEffects.Should().HaveCount(2);
        state.ActiveEffects[0].PowerMod.Should().Be(2);
        state.ActiveEffects[0].ToughnessMod.Should().Be(0);
        state.ActiveEffects[0].UntilEndOfTurn.Should().BeTrue();
        state.ActiveEffects[1].Type.Should().Be(ContinuousEffectType.GrantKeyword);
        state.ActiveEffects[1].GrantedKeyword.Should().Be(Keyword.Mountainwalk);
        state.ActiveEffects[1].UntilEndOfTurn.Should().BeTrue();

        state.DelayedTriggers.Should().HaveCount(1);
        state.DelayedTriggers[0].FireOn.Should().Be(GameEvent.EndStep);
        state.DelayedTriggers[0].ControllerId.Should().Be(player.Id);
    }

    [Fact]
    public async Task PyromancerEffect_Logs()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var pyromancer = new GameCard { Name = "Goblin Pyromancer" };

        var effect = new PyromancerEffect();
        var context = new EffectContext(state, player, pyromancer, handler);

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Goblins get +2/+0 and mountainwalk"));
    }

    // === RearrangeTopEffect ===

    [Fact]
    public async Task RearrangeTopEffect_RearrangesTopCards()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var card1 = new GameCard { Name = "Card A" };
        var card2 = new GameCard { Name = "Card B" };
        var card3 = new GameCard { Name = "Card C" };
        player.Library.Add(card1); // bottom
        player.Library.Add(card2);
        player.Library.Add(card3); // top

        // Choose card2 to go on top
        handler.EnqueueCardChoice(card2.Id);

        var effect = new RearrangeTopEffect(3);
        var source = new GameCard { Name = "Mirri's Guile" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        // All 3 cards should be back in library
        player.Library.Count.Should().Be(3);
        // The chosen card (card2) should be on top
        player.Library.DrawFromTop()!.Id.Should().Be(card2.Id);
    }

    [Fact]
    public async Task RearrangeTopEffect_FewerCardsThanCount_HandlesGracefully()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var card1 = new GameCard { Name = "Card A" };
        player.Library.Add(card1);
        handler.EnqueueCardChoice(card1.Id);

        var effect = new RearrangeTopEffect(3);
        var source = new GameCard { Name = "Mirri's Guile" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Library.Count.Should().Be(1);
    }

    [Fact]
    public async Task RearrangeTopEffect_EmptyLibrary_DoesNothing()
    {
        var (state, player, _, handler, _) = CreateSetup();

        var effect = new RearrangeTopEffect(3);
        var source = new GameCard { Name = "Mirri's Guile" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task RearrangeTopEffect_Logs()
    {
        var (state, player, _, handler, _) = CreateSetup();
        player.Library.Add(new GameCard { Name = "A" });
        player.Library.Add(new GameCard { Name = "B" });

        var effect = new RearrangeTopEffect(3);
        var source = new GameCard { Name = "Mirri's Guile" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("rearranges top 2 cards"));
    }

    // === SylvanLibraryEffect ===

    [Fact]
    public async Task SylvanLibraryEffect_DrawsTwoExtraCards()
    {
        var (state, player, _, handler, _) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Card A" });
        player.Library.Add(new GameCard { Name = "Card B" });
        player.Library.Add(new GameCard { Name = "Card C" });

        var initialHandCount = player.Hand.Count;

        // Choose to put both cards back
        // (default ChooseCard returns first card, we don't enqueue null so it puts cards back)

        var effect = new SylvanLibraryEffect();
        var source = new GameCard { Name = "Sylvan Library" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        // Drew 2, put 2 back
        player.Hand.Count.Should().Be(initialHandCount);
        state.GameLog.Should().Contain(l => l.Contains("draws 2 extra cards"));
    }

    [Fact]
    public async Task SylvanLibraryEffect_KeepCards_Pays4Life()
    {
        var (state, player, _, handler, _) = CreateSetup();
        player.Library.Add(new GameCard { Name = "Card A" });
        player.Library.Add(new GameCard { Name = "Card B" });
        player.Library.Add(new GameCard { Name = "Card C" });

        // Decline to put cards back (null = keep and pay life)
        handler.EnqueueCardChoice(null);

        var effect = new SylvanLibraryEffect();
        var source = new GameCard { Name = "Sylvan Library" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        // Kept 2 cards, paid 4 life each = 8 total (20 - 8 = 12)
        player.Hand.Count.Should().Be(2);
        player.Life.Should().Be(12);
        state.GameLog.Should().Contain(l => l.Contains("pays 4 life"));
    }

    [Fact]
    public async Task SylvanLibraryEffect_EmptyLibrary_DoesNothing()
    {
        var (state, player, _, handler, _) = CreateSetup();

        var effect = new SylvanLibraryEffect();
        var source = new GameCard { Name = "Sylvan Library" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
        player.Life.Should().Be(20);
    }

    [Fact]
    public async Task SylvanLibraryEffect_PutOneBack_KeepOne()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var cardA = new GameCard { Name = "Card A" };
        var cardB = new GameCard { Name = "Card B" };
        player.Library.Add(new GameCard { Name = "Bottom" });
        player.Library.Add(cardA);
        player.Library.Add(cardB);

        // First choice: put cardB back (default picks first)
        // Second choice: decline (keep the remaining card, pay 4 life)
        handler.EnqueueCardChoice(cardB.Id); // Put back cardB
        handler.EnqueueCardChoice(null); // Keep cardA, pay 4 life

        var effect = new SylvanLibraryEffect();
        var source = new GameCard { Name = "Sylvan Library" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(1);
        player.Hand.Cards[0].Id.Should().Be(cardA.Id);
        player.Life.Should().Be(16);
    }
}
