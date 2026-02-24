using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Effects;

public class CarefulStudyEffectTests
{
    private (GameState state, TestDecisionHandler handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        return (state, h1);
    }

    private StackObject CreateSpell(GameState state, Guid controllerId)
    {
        var card = GameCard.Create("Careful Study");
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);
    }

    [Fact]
    public async Task CarefulStudy_Draws2ThenDiscards2()
    {
        var (state, handler) = CreateSetup();
        var libA = new GameCard { Name = "Card A" };
        var libB = new GameCard { Name = "Card B" };
        var libC = new GameCard { Name = "Card C" };
        state.Player1.Library.Add(libA);
        state.Player1.Library.Add(libB);
        state.Player1.Library.Add(libC); // top

        // After drawing 2 (C, B), hand = [C, B]
        // Discard C then B
        handler.EnqueueCardChoice(libC.Id);
        handler.EnqueueCardChoice(libB.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        await new CarefulStudyEffect().ResolveAsync(state, spell, handler);

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == libC.Id);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == libB.Id);
        state.Player1.Library.Count.Should().Be(1);
    }

    [Fact]
    public async Task CarefulStudy_WithExistingHand_DiscardsFromFullHand()
    {
        var (state, handler) = CreateSetup();
        var handCard = new GameCard { Name = "Hand Card" };
        state.Player1.Hand.Add(handCard);

        var libA = new GameCard { Name = "Lib A" };
        var libB = new GameCard { Name = "Lib B" };
        state.Player1.Library.Add(libA);
        state.Player1.Library.Add(libB);

        // After drawing 2 (B, A), hand = [Hand Card, B, A]
        // Discard Hand Card and B
        handler.EnqueueCardChoice(handCard.Id);
        handler.EnqueueCardChoice(libB.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        await new CarefulStudyEffect().ResolveAsync(state, spell, handler);

        state.Player1.Hand.Count.Should().Be(1);
        state.Player1.Hand.Cards[0].Name.Should().Be("Lib A");
        state.Player1.Graveyard.Count.Should().Be(2);
    }

    [Fact]
    public async Task CarefulStudy_EmptyLibrary_NoDraw_NoDiscard()
    {
        var (state, handler) = CreateSetup();
        var spell = CreateSpell(state, state.Player1.Id);
        await new CarefulStudyEffect().ResolveAsync(state, spell, handler);

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Graveyard.Count.Should().Be(0);
    }

    [Fact]
    public async Task CarefulStudy_Only1InLibrary_Draws1Discards1()
    {
        var (state, handler) = CreateSetup();
        var onlyCard = new GameCard { Name = "Only" };
        state.Player1.Library.Add(onlyCard);

        handler.EnqueueCardChoice(onlyCard.Id);

        var spell = CreateSpell(state, state.Player1.Id);
        await new CarefulStudyEffect().ResolveAsync(state, spell, handler);

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Graveyard.Count.Should().Be(1);
        state.Player1.Library.Count.Should().Be(0);
    }

    [Fact]
    public async Task CarefulStudy_LogsDrawAndDiscard()
    {
        var (state, handler) = CreateSetup();
        state.Player1.Library.Add(new GameCard { Name = "A" });
        state.Player1.Library.Add(new GameCard { Name = "B" });

        var spell = CreateSpell(state, state.Player1.Id);
        await new CarefulStudyEffect().ResolveAsync(state, spell, handler);

        state.GameLog.Should().Contain(msg => msg.Contains("draws 2") && msg.Contains("Careful Study"));
        state.GameLog.Should().Contain(msg => msg.Contains("discards 2") && msg.Contains("Careful Study"));
    }
}

public class PeekEffectTests
{
    private (GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return (new GameState(p1, p2), h1, h2);
    }

    [Fact]
    public async Task Peek_LooksAtTargetHandAndDrawsCard()
    {
        var (state, h1, _) = CreateSetup();
        var opponentCard = new GameCard { Name = "Opp Card" };
        state.Player2.Hand.Add(opponentCard);
        var libCard = new GameCard { Name = "Lib Card" };
        state.Player1.Library.Add(libCard);

        var target = new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None);
        var spell = new StackObject(GameCard.Create("Peek"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { target }, 0);

        await new PeekEffect().ResolveAsync(state, spell, h1);

        // Controller drew a card
        state.Player1.Hand.Count.Should().Be(1);
        state.Player1.Hand.Cards[0].Name.Should().Be("Lib Card");
        state.GameLog.Should().Contain(msg => msg.Contains("looks at") && msg.Contains("P2") && msg.Contains("Peek"));
    }

    [Fact]
    public async Task Peek_NoTarget_StillDraws()
    {
        var (state, h1, _) = CreateSetup();
        var libCard = new GameCard { Name = "Lib Card" };
        state.Player1.Library.Add(libCard);

        var spell = new StackObject(GameCard.Create("Peek"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new PeekEffect().ResolveAsync(state, spell, h1);

        state.Player1.Hand.Count.Should().Be(1);
    }

    [Fact]
    public async Task Peek_EmptyLibrary_DoesNotCrash()
    {
        var (state, h1, _) = CreateSetup();

        var target = new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None);
        var spell = new StackObject(GameCard.Create("Peek"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { target }, 0);

        await new PeekEffect().ResolveAsync(state, spell, h1);

        state.Player1.Hand.Count.Should().Be(0);
    }
}

public class AccumulatedKnowledgeEffectTests
{
    [Fact]
    public void AK_NoneInGraveyards_Draws1()
    {
        var state = TestHelper.CreateState();
        for (int i = 0; i < 5; i++)
            state.Player1.Library.Add(new GameCard { Name = $"Card {i}" });

        var spell = new StackObject(GameCard.Create("Accumulated Knowledge"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new AccumulatedKnowledgeEffect().Resolve(state, spell);

        state.Player1.Hand.Count.Should().Be(1);
    }

    [Fact]
    public void AK_OneInGraveyard_Draws2()
    {
        var state = TestHelper.CreateState();
        for (int i = 0; i < 5; i++)
            state.Player1.Library.Add(new GameCard { Name = $"Card {i}" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Accumulated Knowledge" });

        var spell = new StackObject(GameCard.Create("Accumulated Knowledge"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new AccumulatedKnowledgeEffect().Resolve(state, spell);

        state.Player1.Hand.Count.Should().Be(2);
    }

    [Fact]
    public void AK_TwoInGraveyards_AcrossPlayers_Draws3()
    {
        var state = TestHelper.CreateState();
        for (int i = 0; i < 10; i++)
            state.Player1.Library.Add(new GameCard { Name = $"Card {i}" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Accumulated Knowledge" });
        state.Player2.Graveyard.Add(new GameCard { Name = "Accumulated Knowledge" });

        var spell = new StackObject(GameCard.Create("Accumulated Knowledge"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new AccumulatedKnowledgeEffect().Resolve(state, spell);

        state.Player1.Hand.Count.Should().Be(3);
    }

    [Fact]
    public void AK_ThreeInGraveyard_Draws4()
    {
        var state = TestHelper.CreateState();
        for (int i = 0; i < 10; i++)
            state.Player1.Library.Add(new GameCard { Name = $"Card {i}" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Accumulated Knowledge" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Accumulated Knowledge" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Accumulated Knowledge" });

        var spell = new StackObject(GameCard.Create("Accumulated Knowledge"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new AccumulatedKnowledgeEffect().Resolve(state, spell);

        state.Player1.Hand.Count.Should().Be(4);
    }

    [Fact]
    public void AK_LogsCount()
    {
        var state = TestHelper.CreateState();
        for (int i = 0; i < 5; i++)
            state.Player1.Library.Add(new GameCard { Name = $"Card {i}" });
        state.Player1.Graveyard.Add(new GameCard { Name = "Accumulated Knowledge" });

        var spell = new StackObject(GameCard.Create("Accumulated Knowledge"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new AccumulatedKnowledgeEffect().Resolve(state, spell);

        state.GameLog.Should().Contain(msg => msg.Contains("draws 2") && msg.Contains("1 in graveyards"));
    }
}

public class PortentEffectTests
{
    private (GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return (new GameState(p1, p2), h1, h2);
    }

    [Fact]
    public async Task Portent_RearrangesTop3OfTargetLibrary()
    {
        var (state, h1, _) = CreateSetup();
        var cardA = new GameCard { Name = "A" };
        var cardB = new GameCard { Name = "B" };
        var cardC = new GameCard { Name = "C" };
        state.Player2.Library.Add(cardA); // bottom
        state.Player2.Library.Add(cardB);
        state.Player2.Library.Add(cardC); // top

        // Drawn from top: C, B, A. Reversed by reorder: [A, B, C].
        // AddToTop iterates: A on top, then B on top (A pushed down), then C on top.
        // So library top = C, next = B, then A.
        h1.EnqueueReorder(cards => cards.Reverse().ToList(), false);

        var target = new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None);
        var spell = new StackObject(GameCard.Create("Portent"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { target }, 0);

        await new PortentEffect().ResolveAsync(state, spell, h1);

        // With reversed order [A, B, C], AddToTop gives: C on top, B second, A third
        var top = state.Player2.Library.DrawFromTop();
        top!.Name.Should().Be("C");
        var second = state.Player2.Library.DrawFromTop();
        second!.Name.Should().Be("B");
        var third = state.Player2.Library.DrawFromTop();
        third!.Name.Should().Be("A");
    }

    [Fact]
    public async Task Portent_OptionalShuffle()
    {
        var (state, h1, _) = CreateSetup();
        for (int i = 0; i < 5; i++)
            state.Player2.Library.Add(new GameCard { Name = $"Card {i}" });

        // Shuffle the library
        h1.EnqueueReorder(cards => cards.ToList(), true);

        var target = new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None);
        var spell = new StackObject(GameCard.Create("Portent"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { target }, 0);

        await new PortentEffect().ResolveAsync(state, spell, h1);

        state.GameLog.Should().Contain(msg => msg.Contains("shuffles") && msg.Contains("Portent"));
    }

    [Fact]
    public async Task Portent_RegistersDelayedDrawTrigger()
    {
        var (state, h1, _) = CreateSetup();
        state.Player2.Library.Add(new GameCard { Name = "A" });
        state.Player2.Library.Add(new GameCard { Name = "B" });
        state.Player2.Library.Add(new GameCard { Name = "C" });

        h1.EnqueueReorder(cards => cards.ToList(), false);

        var target = new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None);
        var spell = new StackObject(GameCard.Create("Portent"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { target }, 0);

        await new PortentEffect().ResolveAsync(state, spell, h1);

        // A delayed trigger should be registered for Upkeep
        state.DelayedTriggers.Should().HaveCount(1);
        state.DelayedTriggers[0].FireOn.Should().Be(GameEvent.Upkeep);
        state.DelayedTriggers[0].ControllerId.Should().Be(state.Player1.Id);
        state.DelayedTriggers[0].Effect.Should().BeOfType<DrawCardEffect>();
    }

    [Fact]
    public async Task Portent_EmptyLibrary_StillRegistersDelayedDraw()
    {
        var (state, h1, _) = CreateSetup();

        var target = new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None);
        var spell = new StackObject(GameCard.Create("Portent"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { target }, 0);

        await new PortentEffect().ResolveAsync(state, spell, h1);

        // Delayed trigger still registered even if library was empty
        state.DelayedTriggers.Should().HaveCount(1);
        state.GameLog.Should().Contain(msg => msg.Contains("will draw a card"));
    }

    [Fact]
    public async Task Portent_NoTarget_UsesController()
    {
        var (state, h1, _) = CreateSetup();
        state.Player1.Library.Add(new GameCard { Name = "A" });
        state.Player1.Library.Add(new GameCard { Name = "B" });
        state.Player1.Library.Add(new GameCard { Name = "C" });

        h1.EnqueueReorder(cards => cards.ToList(), false);

        var spell = new StackObject(GameCard.Create("Portent"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new PortentEffect().ResolveAsync(state, spell, h1);

        state.GameLog.Should().Contain(msg => msg.Contains("rearranges") && msg.Contains("P1"));
    }
}

public class EnlightenedTutorEffectTests
{
    private (GameState state, TestDecisionHandler handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return (new GameState(p1, p2), h1);
    }

    [Fact]
    public async Task EnlightenedTutor_FindsArtifact_PutsOnTop()
    {
        var (state, handler) = CreateSetup();
        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        state.Player1.Library.Add(creature);
        state.Player1.Library.Add(artifact);

        handler.EnqueueCardChoice(artifact.Id);

        var spell = new StackObject(GameCard.Create("Enlightened Tutor"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new EnlightenedTutorEffect().ResolveAsync(state, spell, handler);

        // Artifact should be on top of library (not in hand!)
        var top = state.Player1.Library.DrawFromTop();
        top!.Id.Should().Be(artifact.Id);
        state.Player1.Hand.Count.Should().Be(0); // Does NOT go to hand
        state.GameLog.Should().Contain(msg => msg.Contains("Sol Ring") && msg.Contains("on top"));
    }

    [Fact]
    public async Task EnlightenedTutor_FindsEnchantment_PutsOnTop()
    {
        var (state, handler) = CreateSetup();
        var enchantment = new GameCard { Name = "Sylvan Library", CardTypes = CardType.Enchantment };
        state.Player1.Library.Add(enchantment);

        handler.EnqueueCardChoice(enchantment.Id);

        var spell = new StackObject(GameCard.Create("Enlightened Tutor"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new EnlightenedTutorEffect().ResolveAsync(state, spell, handler);

        var top = state.Player1.Library.DrawFromTop();
        top!.Id.Should().Be(enchantment.Id);
    }

    [Fact]
    public async Task EnlightenedTutor_NoMatches_ShufflesLibrary()
    {
        var (state, handler) = CreateSetup();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        state.Player1.Library.Add(creature);

        var spell = new StackObject(GameCard.Create("Enlightened Tutor"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new EnlightenedTutorEffect().ResolveAsync(state, spell, handler);

        state.Player1.Hand.Count.Should().Be(0);
        state.GameLog.Should().Contain(msg => msg.Contains("finds no artifact or enchantment"));
    }

    [Fact]
    public async Task EnlightenedTutor_DeclinesToSearch()
    {
        var (state, handler) = CreateSetup();
        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        state.Player1.Library.Add(artifact);

        handler.EnqueueCardChoice(null); // decline

        var spell = new StackObject(GameCard.Create("Enlightened Tutor"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new EnlightenedTutorEffect().ResolveAsync(state, spell, handler);

        state.Player1.Hand.Count.Should().Be(0);
        state.GameLog.Should().Contain(msg => msg.Contains("declines to search"));
    }

    [Fact]
    public async Task EnlightenedTutor_DoesNotFindCreature()
    {
        var (state, handler) = CreateSetup();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        var land = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        var artifact = new GameCard { Name = "Mox", CardTypes = CardType.Artifact };
        state.Player1.Library.Add(creature);
        state.Player1.Library.Add(land);
        state.Player1.Library.Add(artifact);

        handler.EnqueueCardChoice(artifact.Id);

        var spell = new StackObject(GameCard.Create("Enlightened Tutor"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new EnlightenedTutorEffect().ResolveAsync(state, spell, handler);

        var top = state.Player1.Library.DrawFromTop();
        top!.Name.Should().Be("Mox");
    }
}

public class FranticSearchEffectTests
{
    private (GameState state, TestDecisionHandler handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return (new GameState(p1, p2), h1);
    }

    [Fact]
    public async Task FranticSearch_Draws2Discards2UntapsLands()
    {
        var (state, handler) = CreateSetup();
        var libA = new GameCard { Name = "Card A" };
        var libB = new GameCard { Name = "Card B" };
        var libC = new GameCard { Name = "Card C" };
        state.Player1.Library.Add(libA);
        state.Player1.Library.Add(libB);
        state.Player1.Library.Add(libC);

        // Set up 3 tapped lands
        var land1 = new GameCard { Name = "Island", CardTypes = CardType.Land, IsTapped = true };
        var land2 = new GameCard { Name = "Island", CardTypes = CardType.Land, IsTapped = true };
        var land3 = new GameCard { Name = "Island", CardTypes = CardType.Land, IsTapped = true };
        state.Player1.Battlefield.Add(land1);
        state.Player1.Battlefield.Add(land2);
        state.Player1.Battlefield.Add(land3);

        // After drawing C and B, hand = [C, B]
        // Discard C then B
        handler.EnqueueCardChoice(libC.Id);
        handler.EnqueueCardChoice(libB.Id);
        // Untap all 3 lands
        handler.EnqueueCardChoice(land1.Id);
        handler.EnqueueCardChoice(land2.Id);
        handler.EnqueueCardChoice(land3.Id);

        var spell = new StackObject(GameCard.Create("Frantic Search"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new FranticSearchEffect().ResolveAsync(state, spell, handler);

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Graveyard.Count.Should().Be(2);
        land1.IsTapped.Should().BeFalse();
        land2.IsTapped.Should().BeFalse();
        land3.IsTapped.Should().BeFalse();
    }

    [Fact]
    public async Task FranticSearch_UntapsUpTo3_Not4()
    {
        var (state, handler) = CreateSetup();
        state.Player1.Library.Add(new GameCard { Name = "A" });
        state.Player1.Library.Add(new GameCard { Name = "B" });

        var lands = new List<GameCard>();
        for (int i = 0; i < 5; i++)
        {
            var land = new GameCard { Name = $"Land {i}", CardTypes = CardType.Land, IsTapped = true };
            state.Player1.Battlefield.Add(land);
            lands.Add(land);
        }

        // Untap first 3 lands
        handler.EnqueueCardChoice(lands[0].Id);
        handler.EnqueueCardChoice(lands[1].Id);
        handler.EnqueueCardChoice(lands[2].Id);

        var spell = new StackObject(GameCard.Create("Frantic Search"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new FranticSearchEffect().ResolveAsync(state, spell, handler);

        lands[0].IsTapped.Should().BeFalse();
        lands[1].IsTapped.Should().BeFalse();
        lands[2].IsTapped.Should().BeFalse();
        // Remaining 2 lands stay tapped
        lands[3].IsTapped.Should().BeTrue();
        lands[4].IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task FranticSearch_NoTappedLands_SkipsUntap()
    {
        var (state, handler) = CreateSetup();
        state.Player1.Library.Add(new GameCard { Name = "A" });
        state.Player1.Library.Add(new GameCard { Name = "B" });

        // Untapped land
        var land = new GameCard { Name = "Island", CardTypes = CardType.Land, IsTapped = false };
        state.Player1.Battlefield.Add(land);

        var spell = new StackObject(GameCard.Create("Frantic Search"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new FranticSearchEffect().ResolveAsync(state, spell, handler);

        land.IsTapped.Should().BeFalse();
        state.Player1.Hand.Count.Should().Be(0); // drew 2, discarded 2 (default behavior)
    }

    [Fact]
    public async Task FranticSearch_PlayerDeclinesUntap()
    {
        var (state, handler) = CreateSetup();
        var cardA = new GameCard { Name = "A" };
        var cardB = new GameCard { Name = "B" };
        state.Player1.Library.Add(cardA);
        state.Player1.Library.Add(cardB);

        var land = new GameCard { Name = "Island", CardTypes = CardType.Land, IsTapped = true };
        state.Player1.Battlefield.Add(land);

        // Enqueue discard choices first (draws B, A -> discard B, then A)
        handler.EnqueueCardChoice(cardB.Id);
        handler.EnqueueCardChoice(cardA.Id);
        // Then decline to untap
        handler.EnqueueCardChoice(null);

        var spell = new StackObject(GameCard.Create("Frantic Search"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new FranticSearchEffect().ResolveAsync(state, spell, handler);

        land.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task FranticSearch_LogsAllActions()
    {
        var (state, handler) = CreateSetup();
        state.Player1.Library.Add(new GameCard { Name = "A" });
        state.Player1.Library.Add(new GameCard { Name = "B" });

        var land = new GameCard { Name = "Island", CardTypes = CardType.Land, IsTapped = true };
        state.Player1.Battlefield.Add(land);

        handler.EnqueueCardChoice(land.Id);

        var spell = new StackObject(GameCard.Create("Frantic Search"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new FranticSearchEffect().ResolveAsync(state, spell, handler);

        state.GameLog.Should().Contain(msg => msg.Contains("draws 2") && msg.Contains("Frantic Search"));
        state.GameLog.Should().Contain(msg => msg.Contains("discards 2") && msg.Contains("Frantic Search"));
        state.GameLog.Should().Contain(msg => msg.Contains("untaps 1 land") && msg.Contains("Frantic Search"));
    }
}

public class PriceOfProgressEffectTests
{
    [Fact]
    public void PriceOfProgress_NoDamageWithOnlyBasicLands()
    {
        var state = TestHelper.CreateState();
        state.Player1.Battlefield.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        state.Player1.Battlefield.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        state.Player2.Battlefield.Add(new GameCard { Name = "Island", CardTypes = CardType.Land });

        var spell = new StackObject(GameCard.Create("Price of Progress"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new PriceOfProgressEffect().Resolve(state, spell);

        state.Player1.Life.Should().Be(20);
        state.Player2.Life.Should().Be(20);
    }

    [Fact]
    public void PriceOfProgress_DealsDamageForNonbasicLands()
    {
        var state = TestHelper.CreateState();
        state.Player1.Battlefield.Add(new GameCard { Name = "Volcanic Island", CardTypes = CardType.Land });
        state.Player2.Battlefield.Add(new GameCard { Name = "Underground Sea", CardTypes = CardType.Land });
        state.Player2.Battlefield.Add(new GameCard { Name = "Tropical Island", CardTypes = CardType.Land });

        var spell = new StackObject(GameCard.Create("Price of Progress"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new PriceOfProgressEffect().Resolve(state, spell);

        // P1: 1 nonbasic -> 2 damage
        state.Player1.Life.Should().Be(18);
        // P2: 2 nonbasics -> 4 damage
        state.Player2.Life.Should().Be(16);
    }

    [Fact]
    public void PriceOfProgress_MixedLands()
    {
        var state = TestHelper.CreateState();
        state.Player1.Battlefield.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        state.Player1.Battlefield.Add(new GameCard { Name = "Wasteland", CardTypes = CardType.Land });
        state.Player1.Battlefield.Add(new GameCard { Name = "Rishadan Port", CardTypes = CardType.Land });

        var spell = new StackObject(GameCard.Create("Price of Progress"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new PriceOfProgressEffect().Resolve(state, spell);

        // P1: 2 nonbasic (Wasteland, Rishadan Port) -> 4 damage
        state.Player1.Life.Should().Be(16);
        // P2: 0 nonbasic -> 0 damage
        state.Player2.Life.Should().Be(20);
    }

    [Fact]
    public void PriceOfProgress_NoLands_NoDamage()
    {
        var state = TestHelper.CreateState();

        var spell = new StackObject(GameCard.Create("Price of Progress"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        new PriceOfProgressEffect().Resolve(state, spell);

        state.Player1.Life.Should().Be(20);
        state.Player2.Life.Should().Be(20);
    }
}

public class EarthquakeEffectTests
{
    [Fact]
    public async Task Earthquake_DamagesNonflyingCreaturesAndPlayers_DefaultPicksX1()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var bear = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        var bird = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        bird.ActiveKeywords.Add(Keyword.Flying);
        state.Player1.Battlefield.Add(bear);
        state.Player1.Battlefield.Add(bird);

        var oppCreature = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        state.Player2.Battlefield.Add(oppCreature);

        // Put 3 mana in pool
        state.Player1.ManaPool.Add(ManaColor.Red, 3);

        var spell = new StackObject(GameCard.Create("Earthquake"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        // Default TestDecisionHandler picks first option = "X = 1"
        await new EarthquakeEffect().ResolveAsync(state, spell, h1);

        bear.DamageMarked.Should().Be(1);
        bird.DamageMarked.Should().Be(0, "flying creatures are not damaged");
        oppCreature.DamageMarked.Should().Be(1);
        state.Player1.Life.Should().Be(19);
        state.Player2.Life.Should().Be(19);
        // Only 1 mana deducted, 2 remaining
        state.Player1.ManaPool.Total.Should().Be(2);
    }

    [Fact]
    public async Task Earthquake_ChoosesMaxX_DrainsEntirePool()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var bear = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player1.Battlefield.Add(bear);

        // Put 5 mana in pool
        state.Player1.ManaPool.Add(ManaColor.Red, 5);

        var spell = new StackObject(GameCard.Create("Earthquake"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        // Enqueue null choice => defaults to max X (5)
        h1.EnqueueCardChoice(null);

        await new EarthquakeEffect().ResolveAsync(state, spell, h1);

        bear.DamageMarked.Should().Be(5);
        state.Player1.Life.Should().Be(15);
        state.Player2.Life.Should().Be(15);
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task Earthquake_X0_DoesNothing()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        state.Player1.Battlefield.Add(creature);

        var spell = new StackObject(GameCard.Create("Earthquake"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new EarthquakeEffect().ResolveAsync(state, spell, h1);

        creature.DamageMarked.Should().Be(0);
        state.Player1.Life.Should().Be(20);
        state.Player2.Life.Should().Be(20);
    }

    [Fact]
    public async Task Earthquake_DrainsMixedManaPool_ChoosesMax()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        state.Player1.ManaPool.Add(ManaColor.Red, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 3);

        var spell = new StackObject(GameCard.Create("Earthquake"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        // Choose max X by returning null (fallback to max)
        h1.EnqueueCardChoice(null);

        await new EarthquakeEffect().ResolveAsync(state, spell, h1);

        // X = 5 (2 Red + 3 Colorless)
        state.Player1.Life.Should().Be(15);
        state.Player2.Life.Should().Be(15);
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task Earthquake_LogsCorrectly()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        state.Player1.ManaPool.Add(ManaColor.Red, 2);

        var spell = new StackObject(GameCard.Create("Earthquake"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        await new EarthquakeEffect().ResolveAsync(state, spell, h1);

        state.GameLog.Should().Contain(msg => msg.Contains("Earthquake") && msg.Contains("damage"));
    }

    [Fact]
    public async Task Earthquake_OnlyDeductsChosenAmount_NotEntirePool()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();

        // Put 5 mana in pool
        state.Player1.ManaPool.Add(ManaColor.Red, 5);

        var spell = new StackObject(GameCard.Create("Earthquake"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), 0);

        // Default handler picks first option = "X = 1"
        // (no enqueued choice, so default picks first)

        await new EarthquakeEffect().ResolveAsync(state, spell, h1);

        // X = 1: deal 1 damage, deduct 1 mana, leave 4
        state.Player1.Life.Should().Be(19);
        state.Player2.Life.Should().Be(19);
        state.Player1.ManaPool.Total.Should().Be(4);
    }
}

public class CardDefinitionsDrawFilterTests
{
    [Theory]
    [InlineData("Careful Study", "{U}", CardType.Sorcery)]
    [InlineData("Peek", "{U}", CardType.Instant)]
    [InlineData("Accumulated Knowledge", "{1}{U}", CardType.Instant)]
    [InlineData("Portent", "{U}", CardType.Sorcery)]
    [InlineData("Enlightened Tutor", "{W}", CardType.Instant)]
    [InlineData("Frantic Search", "{2}{U}", CardType.Instant)]
    [InlineData("Price of Progress", "{1}{R}", CardType.Instant)]
    [InlineData("Earthquake", "{R}", CardType.Sorcery)]
    public void CardDefinitions_ContainsCard(string name, string expectedManaCost, CardType expectedType)
    {
        CardDefinitions.TryGet(name, out var def).Should().BeTrue($"{name} should be registered");
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ToString().Should().Be(expectedManaCost);
        def.CardTypes.Should().Be(expectedType);
    }

    [Fact]
    public void CardDefinitions_CarefulStudy_HasCarefulStudyEffect()
    {
        CardDefinitions.TryGet("Careful Study", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<CarefulStudyEffect>();
    }

    [Fact]
    public void CardDefinitions_Peek_HasPeekEffect()
    {
        CardDefinitions.TryGet("Peek", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<PeekEffect>();
    }

    [Fact]
    public void CardDefinitions_AccumulatedKnowledge_HasAccumulatedKnowledgeEffect()
    {
        CardDefinitions.TryGet("Accumulated Knowledge", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<AccumulatedKnowledgeEffect>();
    }

    [Fact]
    public void CardDefinitions_Portent_HasPortentEffect()
    {
        CardDefinitions.TryGet("Portent", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<PortentEffect>();
    }

    [Fact]
    public void CardDefinitions_EnlightenedTutor_HasEnlightenedTutorEffect()
    {
        CardDefinitions.TryGet("Enlightened Tutor", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<EnlightenedTutorEffect>();
    }

    [Fact]
    public void CardDefinitions_FranticSearch_HasFranticSearchEffect()
    {
        CardDefinitions.TryGet("Frantic Search", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<FranticSearchEffect>();
    }

    [Fact]
    public void CardDefinitions_PriceOfProgress_HasPriceOfProgressEffect()
    {
        CardDefinitions.TryGet("Price of Progress", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<PriceOfProgressEffect>();
    }

    [Fact]
    public void CardDefinitions_Earthquake_HasEarthquakeEffect()
    {
        CardDefinitions.TryGet("Earthquake", out var def).Should().BeTrue();
        def!.Effect.Should().BeOfType<EarthquakeEffect>();
    }
}
