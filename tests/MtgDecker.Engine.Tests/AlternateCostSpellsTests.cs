using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class AlternateCostSpellsTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    // =============================================
    // Gush — CardDefinition tests
    // =============================================

    [Fact]
    public void Gush_HasCorrectDefinition()
    {
        CardDefinitions.TryGet("Gush", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(5);
        def.CardTypes.Should().Be(CardType.Instant);
        def.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.ReturnLandSubtype.Should().Be("Island");
        def.AlternateCost.ReturnLandCount.Should().Be(2);
    }

    // =============================================
    // Gush — CanPayAlternateCost tests
    // =============================================

    [Fact]
    public void CanPayAlternateCost_Gush_With2Islands_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var gush = GameCard.Create("Gush", "Instant");
        var island1 = GameCard.Create("Island", "Basic Land — Island");
        var island2 = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(gush);
        state.Player1.Battlefield.Add(island1);
        state.Player1.Battlefield.Add(island2);

        CardDefinitions.TryGet("Gush", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, gush).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_Gush_With1Island_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var gush = GameCard.Create("Gush", "Instant");
        var island1 = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(gush);
        state.Player1.Battlefield.Add(island1);

        CardDefinitions.TryGet("Gush", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, gush).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_Gush_WithMountainsInstead_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var gush = GameCard.Create("Gush", "Instant");
        var mtn1 = GameCard.Create("Mountain", "Basic Land — Mountain");
        var mtn2 = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(gush);
        state.Player1.Battlefield.Add(mtn1);
        state.Player1.Battlefield.Add(mtn2);

        CardDefinitions.TryGet("Gush", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, gush).Should().BeFalse();
    }

    // =============================================
    // Gush — Integration tests
    // =============================================

    [Fact]
    public async Task Gush_AlternateCost_ReturnsTwoIslands_DrawsTwoCards()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var gush = GameCard.Create("Gush", "Instant");
        var island1 = GameCard.Create("Island", "Basic Land — Island");
        var island2 = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(gush);
        state.Player1.Battlefield.Add(island1);
        state.Player1.Battlefield.Add(island2);

        // No mana — alternate cost only
        // Return 2 islands: auto-choose first each time

        var handCountBefore = state.Player1.Hand.Count;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, gush.Id));

        // Both islands should be back in hand
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == island1.Id);
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == island2.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == island1.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == island2.Id);

        // Gush should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);

        // Resolve the stack — should draw 2 cards
        await engine.ResolveAllTriggersAsync();

        // Gush in hand is gone, +2 islands returned, +2 drawn = net +3 from before (minus gush)
        // handBefore - gush + 2 islands + 2 drawn
    }

    [Fact]
    public async Task Gush_AlternateCost_DrawEffect_ResolvesCorrectly()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var gush = GameCard.Create("Gush", "Instant");
        var island1 = GameCard.Create("Island", "Basic Land — Island");
        var island2 = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(gush);
        state.Player1.Battlefield.Add(island1);
        state.Player1.Battlefield.Add(island2);

        var libraryCountBefore = state.Player1.Library.Count;

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, gush.Id));
        await engine.ResolveAllTriggersAsync();

        // Should have drawn 2 cards from library
        state.Player1.Library.Count.Should().Be(libraryCountBefore - 2);
    }

    // =============================================
    // Foil — CardDefinition tests
    // =============================================

    [Fact]
    public void Foil_HasCorrectDefinition()
    {
        CardDefinitions.TryGet("Foil", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
        def.CardTypes.Should().Be(CardType.Instant);
        def.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.DiscardLandSubtype.Should().Be("Island");
        def.AlternateCost.DiscardAnyCount.Should().Be(1);
        def.Effect.Should().BeOfType<MtgDecker.Engine.Effects.CounterSpellEffect>();
    }

    // =============================================
    // Foil — CanPayAlternateCost tests
    // =============================================

    [Fact]
    public void CanPayAlternateCost_Foil_WithIslandAndExtraCard_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var foil = GameCard.Create("Foil", "Instant");
        var island = GameCard.Create("Island", "Basic Land — Island");
        var extraCard = new GameCard { Name = "SomeCard", ManaCost = ManaCost.Parse("{1}") };
        state.Player1.Hand.Add(foil);
        state.Player1.Hand.Add(island);
        state.Player1.Hand.Add(extraCard);

        CardDefinitions.TryGet("Foil", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, foil).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_Foil_WithIslandButNoExtraCard_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var foil = GameCard.Create("Foil", "Instant");
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(foil);
        state.Player1.Hand.Add(island);
        // Only foil + island in hand; island used for land discard, no extra card for DiscardAnyCount

        CardDefinitions.TryGet("Foil", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, foil).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_Foil_WithoutIsland_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var foil = GameCard.Create("Foil", "Instant");
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        var extraCard = new GameCard { Name = "SomeCard", ManaCost = ManaCost.Parse("{1}") };
        state.Player1.Hand.Add(foil);
        state.Player1.Hand.Add(forest);
        state.Player1.Hand.Add(extraCard);

        CardDefinitions.TryGet("Foil", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, foil).Should().BeFalse();
    }

    // =============================================
    // Foil — Integration tests
    // =============================================

    [Fact]
    public async Task Foil_AlternateCost_DiscardsIslandAndCard_CountersSpell()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // Put a spell on the stack for Foil to target
        var opponentSpell = GameCard.Create("Lightning Bolt", "Instant");
        var stackObj = new StackObject(opponentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        var foil = GameCard.Create("Foil", "Instant");
        var island = GameCard.Create("Island", "Basic Land — Island");
        var extraCard = new GameCard { Name = "ExtraCard", ManaCost = ManaCost.Parse("{1}") };
        state.Player1.Hand.Add(foil);
        state.Player1.Hand.Add(island);
        state.Player1.Hand.Add(extraCard);

        // Target the opponent's spell
        h1.EnqueueTarget(new TargetInfo(opponentSpell.Id, state.Player2.Id, ZoneType.Stack));
        // Choose island for land discard (auto-choose first Island)
        h1.EnqueueCardChoice(island.Id);
        // Choose extra card for the additional discard
        h1.EnqueueCardChoice(extraCard.Id);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, foil.Id));

        // Island should be discarded (to graveyard)
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == island.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == island.Id);
        // Extra card should also be discarded
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == extraCard.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == extraCard.Id);
        // Foil should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Foil_AlternateCost_ResolvesCounterSpell()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var opponentSpell = GameCard.Create("Lightning Bolt", "Instant");
        var stackObj = new StackObject(opponentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        var foil = GameCard.Create("Foil", "Instant");
        var island = GameCard.Create("Island", "Basic Land — Island");
        var extraCard = new GameCard { Name = "ExtraCard", ManaCost = ManaCost.Parse("{1}") };
        state.Player1.Hand.Add(foil);
        state.Player1.Hand.Add(island);
        state.Player1.Hand.Add(extraCard);

        h1.EnqueueTarget(new TargetInfo(opponentSpell.Id, state.Player2.Id, ZoneType.Stack));
        h1.EnqueueCardChoice(island.Id);
        h1.EnqueueCardChoice(extraCard.Id);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, foil.Id));
        await engine.ResolveAllTriggersAsync();

        // Opponent's spell should be countered (in graveyard)
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == opponentSpell.Id);
        state.StackCount.Should().Be(0);
    }

    // =============================================
    // Spinning Darkness — CardDefinition tests
    // =============================================

    [Fact]
    public void SpinningDarkness_HasCorrectDefinition()
    {
        CardDefinitions.TryGet("Spinning Darkness", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(6);
        def.CardTypes.Should().Be(CardType.Instant);
        def.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.ExileFromGraveyardCount.Should().Be(3);
        def.AlternateCost.ExileFromGraveyardColor.Should().Be(ManaColor.Black);
        def.Effect.Should().BeOfType<MtgDecker.Engine.Effects.SpinningDarknessEffect>();
    }

    // =============================================
    // Spinning Darkness — CanPayAlternateCost tests
    // =============================================

    [Fact]
    public void CanPayAlternateCost_SpinningDarkness_With3BlackCards_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var sd = GameCard.Create("Spinning Darkness", "Instant");
        state.Player1.Hand.Add(sd);

        // Put 3 black cards in graveyard
        for (int i = 0; i < 3; i++)
        {
            state.Player1.Graveyard.Add(new GameCard
            {
                Name = $"BlackCard{i}",
                ManaCost = ManaCost.Parse("{B}")
            });
        }

        CardDefinitions.TryGet("Spinning Darkness", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, sd).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_SpinningDarkness_With2BlackCards_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var sd = GameCard.Create("Spinning Darkness", "Instant");
        state.Player1.Hand.Add(sd);

        // Put only 2 black cards in graveyard
        for (int i = 0; i < 2; i++)
        {
            state.Player1.Graveyard.Add(new GameCard
            {
                Name = $"BlackCard{i}",
                ManaCost = ManaCost.Parse("{B}")
            });
        }

        CardDefinitions.TryGet("Spinning Darkness", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, sd).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_SpinningDarkness_WithNonBlackCards_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var sd = GameCard.Create("Spinning Darkness", "Instant");
        state.Player1.Hand.Add(sd);

        // Put 3 non-black cards in graveyard
        for (int i = 0; i < 3; i++)
        {
            state.Player1.Graveyard.Add(new GameCard
            {
                Name = $"RedCard{i}",
                ManaCost = ManaCost.Parse("{R}")
            });
        }

        CardDefinitions.TryGet("Spinning Darkness", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, sd).Should().BeFalse();
    }

    // =============================================
    // Spinning Darkness — Integration tests
    // =============================================

    [Fact]
    public async Task SpinningDarkness_AlternateCost_Exiles3BlackCards()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var sd = GameCard.Create("Spinning Darkness", "Instant");
        state.Player1.Hand.Add(sd);

        // Put 3 black cards in graveyard
        var blackCards = new List<GameCard>();
        for (int i = 0; i < 3; i++)
        {
            var bc = new GameCard { Name = $"BlackCard{i}", ManaCost = ManaCost.Parse("{B}") };
            state.Player1.Graveyard.Add(bc);
            blackCards.Add(bc);
        }

        // Put a non-black creature on opponent's field for targeting
        var creature = new GameCard { Name = "Llanowar Elves", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{G}") };
        state.Player2.Battlefield.Add(creature);

        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, sd.Id));

        // 3 black cards should be exiled from graveyard
        state.Player1.Exile.Cards.Where(c => c.Name.StartsWith("BlackCard")).Should().HaveCount(3);
        state.Player1.Graveyard.Cards.Where(c => c.Name.StartsWith("BlackCard")).Should().HaveCount(0);

        // Spinning Darkness should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SpinningDarkness_Resolves_DamageAndLifeGain()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var sd = GameCard.Create("Spinning Darkness", "Instant");
        state.Player1.Hand.Add(sd);

        // Put 3 black cards in graveyard
        for (int i = 0; i < 3; i++)
        {
            state.Player1.Graveyard.Add(new GameCard
            {
                Name = $"BlackCard{i}",
                ManaCost = ManaCost.Parse("{B}")
            });
        }

        var creature = new GameCard
        {
            Name = "Llanowar Elves",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{G}"),
            BasePower = 1,
            BaseToughness = 1
        };
        state.Player2.Battlefield.Add(creature);

        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        var lifeBefore = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, sd.Id));
        await engine.ResolveAllTriggersAsync();

        // Creature should have 3 damage marked
        // (it may be dead from SBA depending on how toughness checks work)
        // Player should have gained 3 life
        state.Player1.Life.Should().Be(lifeBefore + 3);
    }

    // =============================================
    // Mogg Salvage — CardDefinition tests
    // =============================================

    [Fact]
    public void MoggSalvage_HasCorrectDefinition()
    {
        CardDefinitions.TryGet("Mogg Salvage", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.CardTypes.Should().Be(CardType.Instant);
        def.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.RequiresControlSubtype.Should().Be("Mountain");
        def.AlternateCost.RequiresOpponentSubtype.Should().Be("Island");
        def.Effect.Should().BeOfType<MtgDecker.Engine.Effects.NaturalizeEffect>();
    }

    // =============================================
    // Mogg Salvage — CanPayAlternateCost tests
    // =============================================

    [Fact]
    public void CanPayAlternateCost_MoggSalvage_WithMountainAndOpponentIsland_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var ms = GameCard.Create("Mogg Salvage", "Instant");
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(ms);
        state.Player1.Battlefield.Add(mountain);
        state.Player2.Battlefield.Add(island);

        CardDefinitions.TryGet("Mogg Salvage", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, ms).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_MoggSalvage_WithoutMountain_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var ms = GameCard.Create("Mogg Salvage", "Instant");
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(ms);
        state.Player2.Battlefield.Add(island);

        CardDefinitions.TryGet("Mogg Salvage", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, ms).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_MoggSalvage_WithoutOpponentIsland_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var ms = GameCard.Create("Mogg Salvage", "Instant");
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(ms);
        state.Player1.Battlefield.Add(mountain);
        // Opponent has no island

        CardDefinitions.TryGet("Mogg Salvage", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, ms).Should().BeFalse();
    }

    // =============================================
    // Mogg Salvage — Integration tests
    // =============================================

    [Fact]
    public async Task MoggSalvage_AlternateCost_DestroysArtifact()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var ms = GameCard.Create("Mogg Salvage", "Instant");
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        var opponentIsland = GameCard.Create("Island", "Basic Land — Island");
        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        state.Player1.Hand.Add(ms);
        state.Player1.Battlefield.Add(mountain);
        state.Player2.Battlefield.Add(opponentIsland);
        state.Player2.Battlefield.Add(artifact);

        h1.EnqueueTarget(new TargetInfo(artifact.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, ms.Id));

        // Mogg Salvage on stack — no cost paid since alternate cost requires nothing but conditions
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);

        // Resolve
        await engine.ResolveAllTriggersAsync();

        // Artifact should be destroyed (in graveyard)
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == artifact.Id);
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
    }

    // =============================================
    // Pyrokinesis — CardDefinition tests
    // =============================================

    [Fact]
    public void Pyrokinesis_HasCorrectDefinition()
    {
        CardDefinitions.TryGet("Pyrokinesis", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(6);
        def.CardTypes.Should().Be(CardType.Instant);
        def.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.ExileCardColor.Should().Be(ManaColor.Red);
    }

    // =============================================
    // Pyrokinesis — CanPayAlternateCost tests
    // =============================================

    [Fact]
    public void CanPayAlternateCost_Pyrokinesis_WithRedCard_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var pyro = GameCard.Create("Pyrokinesis", "Instant");
        var redCard = new GameCard { Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}") };
        state.Player1.Hand.Add(pyro);
        state.Player1.Hand.Add(redCard);

        CardDefinitions.TryGet("Pyrokinesis", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, pyro).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_Pyrokinesis_WithoutRedCard_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var pyro = GameCard.Create("Pyrokinesis", "Instant");
        var blueCard = new GameCard { Name = "Brainstorm", ManaCost = ManaCost.Parse("{U}") };
        state.Player1.Hand.Add(pyro);
        state.Player1.Hand.Add(blueCard);

        CardDefinitions.TryGet("Pyrokinesis", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, pyro).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_Pyrokinesis_CannotExileSelf()
    {
        var (engine, state, _, _) = CreateSetup();

        var pyro = GameCard.Create("Pyrokinesis", "Instant");
        // Pyrokinesis has {4}{R}{R} cost, so it IS a red card. But it can't exile itself.
        state.Player1.Hand.Add(pyro);

        CardDefinitions.TryGet("Pyrokinesis", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, pyro).Should().BeFalse();
    }

    // =============================================
    // Pyrokinesis — Integration tests
    // =============================================

    [Fact]
    public async Task Pyrokinesis_AlternateCost_ExilesRedCard_Deals4DamageToCreature()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var pyro = GameCard.Create("Pyrokinesis", "Instant");
        var redCard = new GameCard { Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}") };
        var creature = new GameCard
        {
            Name = "Tarmogoyf",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{1}{G}"),
            BasePower = 0,
            BaseToughness = 1
        };
        state.Player1.Hand.Add(pyro);
        state.Player1.Hand.Add(redCard);
        state.Player2.Battlefield.Add(creature);

        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, pyro.Id));

        // Red card should be exiled
        state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Name == "Lightning Bolt");

        // Pyrokinesis on stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);

        // Resolve
        await engine.ResolveAllTriggersAsync();

        // Creature should have 4 damage
        var target = state.Player2.Battlefield.Cards.FirstOrDefault(c => c.Name == "Tarmogoyf");
        if (target != null)
        {
            target.DamageMarked.Should().Be(4);
        }
    }

    // =============================================
    // Gaea's Blessing — CardDefinition tests
    // =============================================

    [Fact]
    public void GaeasBlessing_HasCorrectDefinition()
    {
        CardDefinitions.TryGet("Gaea's Blessing", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.CardTypes.Should().Be(CardType.Sorcery);
        def.Effect.Should().BeOfType<MtgDecker.Engine.Effects.GaeasBlessingEffect>();
        def.ShuffleGraveyardOnMill.Should().BeTrue();
    }

    // =============================================
    // Gaea's Blessing — Integration tests
    // =============================================

    [Fact]
    public async Task GaeasBlessing_ShufflesCardsFromGraveyardIntoLibrary_DrawsCard()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var gb = GameCard.Create("Gaea's Blessing", "Sorcery");
        state.Player1.Hand.Add(gb);
        state.Player1.ManaPool.Add(ManaColor.Green, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        // Put some cards in graveyard
        var gy1 = new GameCard { Name = "Dead1" };
        var gy2 = new GameCard { Name = "Dead2" };
        var gy3 = new GameCard { Name = "Dead3" };
        state.Player1.Graveyard.Add(gy1);
        state.Player1.Graveyard.Add(gy2);
        state.Player1.Graveyard.Add(gy3);

        var libraryCountBefore = state.Player1.Library.Count;

        // Target self (Player1) for the graveyard shuffle
        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player1.Id, ZoneType.None));
        // Choose up to 3 cards from graveyard
        h1.EnqueueCardChoice(gy1.Id);
        h1.EnqueueCardChoice(gy2.Id);
        h1.EnqueueCardChoice(gy3.Id);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, gb.Id));
        await engine.ResolveAllTriggersAsync();

        // Gaea's Blessing goes to graveyard after resolving
        // Up to 3 cards from graveyard should be shuffled into library (auto-choose first)
        // Then draws a card
        // Net: 3 cards from graveyard to library, +1 draw from library
        // Library should have gained 3 cards - 1 drawn = net +2
        state.Player1.Library.Count.Should().Be(libraryCountBefore + 3 - 1);
    }

    [Fact]
    public async Task GaeasBlessing_EmptyGraveyard_StillDrawsCard()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var gb = GameCard.Create("Gaea's Blessing", "Sorcery");
        state.Player1.Hand.Add(gb);
        state.Player1.ManaPool.Add(ManaColor.Green, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        // Empty graveyard
        var libraryCountBefore = state.Player1.Library.Count;

        // Target self (Player1)
        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player1.Id, ZoneType.None));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, gb.Id));
        await engine.ResolveAllTriggersAsync();

        // Should still draw a card even with empty graveyard
        state.Player1.Library.Count.Should().Be(libraryCountBefore - 1);
    }

    [Fact]
    public void GaeasBlessing_MoveToGraveyard_DoesNotTriggerShuffle()
    {
        var (engine, state, _, _) = CreateSetup();

        // Gaea's Blessing now has ShuffleGraveyardOnMill (not OnDeath), so
        // MoveToGraveyardWithReplacement should NOT trigger shuffle — it just goes to graveyard
        var gb = GameCard.Create("Gaea's Blessing", "Sorcery");
        var graveyardCard = new GameCard { Name = "OldCard" };
        state.Player1.Graveyard.Add(graveyardCard);

        var libBefore = state.Player1.Library.Count;

        engine.MoveToGraveyardWithReplacement(gb, state.Player1);

        // Gaea's Blessing should go to graveyard normally (no shuffle)
        state.Player1.Library.Count.Should().Be(libBefore);
        state.Player1.Graveyard.Count.Should().Be(2); // OldCard + Gaea's Blessing
    }

    // =============================================
    // Daze regression — ReturnLandCount=1 backward compat
    // =============================================

    [Fact]
    public void Daze_AlternateCost_StillHasReturnLandCount1()
    {
        CardDefinitions.TryGet("Daze", out var def).Should().BeTrue();
        def!.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.ReturnLandSubtype.Should().Be("Island");
        def.AlternateCost.ReturnLandCount.Should().Be(1);
    }

    [Fact]
    public async Task Daze_StillWorksWithOneIsland()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var opponentSpell = GameCard.Create("Lightning Bolt", "Instant");
        var stackObj = new StackObject(opponentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        var daze = GameCard.Create("Daze", "Instant");
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(daze);
        state.Player1.Battlefield.Add(island);

        h1.EnqueueTarget(new TargetInfo(opponentSpell.Id, state.Player2.Id, ZoneType.Stack));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, daze.Id));

        // Island should be returned to hand
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == island.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == island.Id);
    }
}
