using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase9AlternateCostTests
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

    // --- CardDefinition registration tests ---

    [Fact]
    public void ForceOfWill_HasAlternateCost()
    {
        CardDefinitions.TryGet("Force of Will", out var def).Should().BeTrue();
        def!.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.LifeCost.Should().Be(1);
        def.AlternateCost.ExileCardColor.Should().Be(ManaColor.Blue);
    }

    [Fact]
    public void Daze_HasAlternateCost()
    {
        CardDefinitions.TryGet("Daze", out var def).Should().BeTrue();
        def!.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.ReturnLandSubtype.Should().Be("Island");
    }

    [Fact]
    public void Fireblast_HasAlternateCost()
    {
        CardDefinitions.TryGet("Fireblast", out var def).Should().BeTrue();
        def!.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.SacrificeLandSubtype.Should().Be("Mountain");
        def.AlternateCost.SacrificeLandCount.Should().Be(2);
    }

    [Fact]
    public void SnuffOut_HasAlternateCost()
    {
        CardDefinitions.TryGet("Snuff Out", out var def).Should().BeTrue();
        def!.AlternateCost.Should().NotBeNull();
        def.AlternateCost!.LifeCost.Should().Be(4);
        def.AlternateCost.RequiresControlSubtype.Should().Be("Swamp");
    }

    [Fact]
    public void MoxDiamond_HasManaAbility()
    {
        CardDefinitions.TryGet("Mox Diamond", out var def).Should().BeTrue();
        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Choice);
        def.ManaAbility.ChoiceColors.Should().Contain(ManaColor.White);
        def.ManaAbility.ChoiceColors.Should().Contain(ManaColor.Blue);
        def.ManaAbility.ChoiceColors.Should().Contain(ManaColor.Black);
        def.ManaAbility.ChoiceColors.Should().Contain(ManaColor.Red);
        def.ManaAbility.ChoiceColors.Should().Contain(ManaColor.Green);
    }

    // --- CanPayAlternateCost tests ---

    [Fact]
    public void CanPayAlternateCost_ForceOfWill_WithBlueCard_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var fow = GameCard.Create("Force of Will", "Instant");
        var blueCard = new GameCard { Name = "Brainstorm", ManaCost = ManaCost.Parse("{U}") };
        state.Player1.Hand.Add(fow);
        state.Player1.Hand.Add(blueCard);

        CardDefinitions.TryGet("Force of Will", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, fow).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_ForceOfWill_WithoutBlueCard_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var fow = GameCard.Create("Force of Will", "Instant");
        var redCard = new GameCard { Name = "Lightning Bolt", ManaCost = ManaCost.Parse("{R}") };
        state.Player1.Hand.Add(fow);
        state.Player1.Hand.Add(redCard);

        CardDefinitions.TryGet("Force of Will", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, fow).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_ForceOfWill_CannotExileSelf()
    {
        var (engine, state, _, _) = CreateSetup();

        // Force of Will is the only blue card — should not be able to exile itself
        var fow = GameCard.Create("Force of Will", "Instant");
        state.Player1.Hand.Add(fow);

        CardDefinitions.TryGet("Force of Will", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, fow).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_ForceOfWill_LifeTooLow_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var fow = GameCard.Create("Force of Will", "Instant");
        var blueCard = new GameCard { Name = "Brainstorm", ManaCost = ManaCost.Parse("{U}") };
        state.Player1.Hand.Add(fow);
        state.Player1.Hand.Add(blueCard);
        state.Player1.AdjustLife(-19); // Life = 1, which is <= LifeCost of 1

        CardDefinitions.TryGet("Force of Will", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, fow).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_Daze_WithIsland_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var daze = GameCard.Create("Daze", "Instant");
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(daze);
        state.Player1.Battlefield.Add(island);

        CardDefinitions.TryGet("Daze", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, daze).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_Daze_WithoutIsland_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var daze = GameCard.Create("Daze", "Instant");
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(daze);
        state.Player1.Battlefield.Add(mountain);

        CardDefinitions.TryGet("Daze", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, daze).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_Fireblast_With2Mountains_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var fireblast = GameCard.Create("Fireblast", "Instant");
        var mtn1 = GameCard.Create("Mountain", "Basic Land — Mountain");
        var mtn2 = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(fireblast);
        state.Player1.Battlefield.Add(mtn1);
        state.Player1.Battlefield.Add(mtn2);

        CardDefinitions.TryGet("Fireblast", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, fireblast).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_Fireblast_With1Mountain_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var fireblast = GameCard.Create("Fireblast", "Instant");
        var mtn1 = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(fireblast);
        state.Player1.Battlefield.Add(mtn1);

        CardDefinitions.TryGet("Fireblast", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, fireblast).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_SnuffOut_WithSwamp_ReturnsTrue()
    {
        var (engine, state, _, _) = CreateSetup();

        var snuff = GameCard.Create("Snuff Out", "Instant");
        var swamp = GameCard.Create("Swamp", "Basic Land — Swamp");
        state.Player1.Hand.Add(snuff);
        state.Player1.Battlefield.Add(swamp);

        CardDefinitions.TryGet("Snuff Out", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, snuff).Should().BeTrue();
    }

    [Fact]
    public void CanPayAlternateCost_SnuffOut_WithoutSwamp_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var snuff = GameCard.Create("Snuff Out", "Instant");
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(snuff);
        state.Player1.Battlefield.Add(mountain);

        CardDefinitions.TryGet("Snuff Out", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, snuff).Should().BeFalse();
    }

    [Fact]
    public void CanPayAlternateCost_SnuffOut_LifeTooLow_ReturnsFalse()
    {
        var (engine, state, _, _) = CreateSetup();

        var snuff = GameCard.Create("Snuff Out", "Instant");
        var swamp = GameCard.Create("Swamp", "Basic Land — Swamp");
        state.Player1.Hand.Add(snuff);
        state.Player1.Battlefield.Add(swamp);
        state.Player1.AdjustLife(-16); // Life = 4, which is <= LifeCost of 4

        CardDefinitions.TryGet("Snuff Out", out var def).Should().BeTrue();
        engine.CanPayAlternateCost(def!.AlternateCost!, state.Player1, snuff).Should().BeFalse();
    }

    // --- Integration tests: CastSpell with alternate cost ---

    [Fact]
    public async Task ForceOfWill_AlternateCost_ExilesBlueCardAndPaysLife()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // Put a spell on the stack for FoW to target
        var opponentSpell = GameCard.Create("Lightning Bolt", "Instant");
        var stackObj = new StackObject(opponentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        var fow = GameCard.Create("Force of Will", "Instant");
        var brainstorm = new GameCard { Name = "Brainstorm", ManaCost = ManaCost.Parse("{U}") };
        state.Player1.Hand.Add(fow);
        state.Player1.Hand.Add(brainstorm);

        // No mana in pool — only alternate cost available
        // Choose card: skip choosing (null) = use alternate cost (since both not available, only alt available, no choice prompt)
        // Exile: auto-choose first blue card (Brainstorm)
        // Target: choose the opponent's spell on the stack
        h1.EnqueueTarget(new TargetInfo(opponentSpell.Id, state.Player2.Id, ZoneType.Stack));

        var startLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, fow.Id));

        // FoW should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
        // Brainstorm should be exiled
        state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Brainstorm");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Name == "Brainstorm");
        // Life should be reduced by 1
        state.Player1.Life.Should().Be(startLife - 1);
        // FoW should not be in hand anymore
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == fow.Id);
    }

    [Fact]
    public async Task ForceOfWill_ManaAvailable_PlayerChoosesMana()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // Put a spell on the stack
        var opponentSpell = GameCard.Create("Lightning Bolt", "Instant");
        var stackObj = new StackObject(opponentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        var fow = GameCard.Create("Force of Will", "Instant");
        var brainstorm = new GameCard { Name = "Brainstorm", ManaCost = ManaCost.Parse("{U}") };
        state.Player1.Hand.Add(fow);
        state.Player1.Hand.Add(brainstorm);

        // Add enough mana to pay normally: {3}{U}{U}
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 3);

        // Both mana and alternate cost available — player chooses mana (select the card = pay mana)
        h1.EnqueueCardChoice(fow.Id); // Choose card = pay mana
        // Target
        h1.EnqueueTarget(new TargetInfo(opponentSpell.Id, state.Player2.Id, ZoneType.Stack));

        var startLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, fow.Id));

        // Brainstorm should still be in hand (not exiled)
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Brainstorm");
        // Life should be unchanged
        state.Player1.Life.Should().Be(startLife);
        // Mana pool should be depleted
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task ForceOfWill_BothAvailable_PlayerChoosesAlternate()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // Put a spell on the stack
        var opponentSpell = GameCard.Create("Lightning Bolt", "Instant");
        var stackObj = new StackObject(opponentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        var fow = GameCard.Create("Force of Will", "Instant");
        var brainstorm = new GameCard { Name = "Brainstorm", ManaCost = ManaCost.Parse("{U}") };
        state.Player1.Hand.Add(fow);
        state.Player1.Hand.Add(brainstorm);

        // Add enough mana
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 3);

        // Both available — player chooses alternate (null = use alternate)
        h1.EnqueueCardChoice(null); // null = use alternate cost
        // Exile choice: auto-choose first blue card
        // Target
        h1.EnqueueTarget(new TargetInfo(opponentSpell.Id, state.Player2.Id, ZoneType.Stack));

        var startLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, fow.Id));

        // Brainstorm should be exiled
        state.Player1.Exile.Cards.Should().Contain(c => c.Name == "Brainstorm");
        // Life should be reduced by 1
        state.Player1.Life.Should().Be(startLife - 1);
        // Mana pool should still have mana (not spent)
        state.Player1.ManaPool.Total.Should().Be(5);
    }

    [Fact]
    public async Task Daze_AlternateCost_ReturnsIslandToHand()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // Put a spell on the stack
        var opponentSpell = GameCard.Create("Lightning Bolt", "Instant");
        var stackObj = new StackObject(opponentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        var daze = GameCard.Create("Daze", "Instant");
        var island = GameCard.Create("Island", "Basic Land — Island");
        state.Player1.Hand.Add(daze);
        state.Player1.Battlefield.Add(island);

        // No mana — alternate cost only
        // Return island: auto-choose first island
        h1.EnqueueTarget(new TargetInfo(opponentSpell.Id, state.Player2.Id, ZoneType.Stack));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, daze.Id));

        // Island should be back in hand
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == island.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == island.Id);
        // Daze should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Fireblast_AlternateCost_SacrificesTwoMountains()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var fireblast = GameCard.Create("Fireblast", "Instant");
        var mtn1 = GameCard.Create("Mountain", "Basic Land — Mountain");
        var mtn2 = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Hand.Add(fireblast);
        state.Player1.Battlefield.Add(mtn1);
        state.Player1.Battlefield.Add(mtn2);

        // No mana — alternate cost only
        // Sacrifice: auto-choose first mountain each time
        // Target: opponent player
        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, fireblast.Id));

        // Both mountains should be in graveyard
        state.Player1.Graveyard.Cards.Where(c => c.Name == "Mountain").Should().HaveCount(2);
        state.Player1.Battlefield.Cards.Where(c => c.Name == "Mountain").Should().HaveCount(0);
        // Fireblast should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SnuffOut_AlternateCost_Pays4Life()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var snuff = GameCard.Create("Snuff Out", "Instant");
        var swamp = GameCard.Create("Swamp", "Basic Land — Swamp");
        state.Player1.Hand.Add(snuff);
        state.Player1.Battlefield.Add(swamp);

        // Put a non-black creature on opponent's battlefield for targeting
        var creature = new GameCard { Name = "Llanowar Elves", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{G}") };
        state.Player2.Battlefield.Add(creature);

        // No mana — alternate cost only
        h1.EnqueueTarget(new TargetInfo(creature.Id, state.Player2.Id, ZoneType.Battlefield));

        var startLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, snuff.Id));

        // Life should be reduced by 4
        state.Player1.Life.Should().Be(startLife - 4);
        // Snuff Out should be on the stack
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CastSpell_NeitherManaNoAlternate_Rejected()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // Put a spell on the stack for targeting
        var opponentSpell = GameCard.Create("Lightning Bolt", "Instant");
        var stackObj = new StackObject(opponentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(stackObj);

        var fow = GameCard.Create("Force of Will", "Instant");
        state.Player1.Hand.Add(fow);
        // No blue cards in hand, no mana — neither cost payable

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, fow.Id));

        // FoW should still be in hand
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == fow.Id);
    }

    // --- Mox Diamond ETB tests ---

    [Fact]
    public async Task MoxDiamond_ETB_DiscardLand_StaysOnBattlefield()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var mox = GameCard.Create("Mox Diamond", "Artifact");
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(mox);
        state.Player1.Hand.Add(forest);

        // Mox costs {0} — cast it via CastSpell
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, mox.Id));

        // Resolve the stack (spell resolution + ETB trigger)
        await engine.ResolveAllTriggersAsync();

        // Forest should be discarded
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == forest.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == forest.Id);
        // Mox should stay on battlefield
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == mox.Id);
    }

    [Fact]
    public async Task MoxDiamond_ETB_NoLand_GoesToGraveyard()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var mox = GameCard.Create("Mox Diamond", "Artifact");
        state.Player1.Hand.Add(mox);
        // No lands in hand

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, mox.Id));

        // Resolve the stack (spell resolution + ETB trigger)
        await engine.ResolveAllTriggersAsync();

        // Mox should be in graveyard (sacrificed)
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == mox.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == mox.Id);
    }

    [Fact]
    public async Task MoxDiamond_CanTapForMana_AfterDiscard()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var mox = GameCard.Create("Mox Diamond", "Artifact");
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(mox);
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, mox.Id));
        await engine.ResolveAllTriggersAsync();

        // Mox should be on battlefield
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == mox.Id);

        // Tap mox for mana — choose red
        h1.EnqueueManaColor(ManaColor.Red);
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mox.Id));

        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
    }
}
