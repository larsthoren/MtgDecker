using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class GameEngineUndoTests
{
    private GameEngine CreateEngine(out GameState state, out Player player1, out Player player2)
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        player1 = new Player(Guid.NewGuid(), "Alice", p1Handler);
        player2 = new Player(Guid.NewGuid(), "Bob", p2Handler);
        state = new GameState(player1, player2);
        return new GameEngine(state);
    }

    [Fact]
    public async Task UndoTap_UnspentMana_UntapsAndRemovesMana()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        forest.ManaAbility = ManaAbility.Fixed(ManaColor.Green);
        p1.Battlefield.Add(forest);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest.Id));
        forest.IsTapped.Should().BeTrue();
        p1.ManaPool[ManaColor.Green].Should().Be(1);

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeTrue();
        forest.IsTapped.Should().BeFalse();
        p1.ManaPool[ManaColor.Green].Should().Be(0);
    }

    [Fact]
    public async Task UndoTap_AfterManaSpent_Rejected()
    {
        var engine = CreateEngine(out var state, out var p1, out _);
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        mountain.ManaAbility = ManaAbility.Fixed(ManaColor.Red);
        p1.Battlefield.Add(mountain);

        // Tap mountain for R
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, mountain.Id));

        // Simulate mana being spent by clearing pending taps
        p1.PendingManaTaps.Clear();

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeFalse();
        mountain.IsTapped.Should().BeTrue(); // Still tapped
        state.GameLog.Should().Contain(l => l.Contains("already spent"));
    }

    [Fact]
    public async Task UndoTap_MultipleTaps_UndoesInOrder()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var forest1 = GameCard.Create("Forest", "Basic Land — Forest");
        forest1.ManaAbility = ManaAbility.Fixed(ManaColor.Green);
        var forest2 = GameCard.Create("Forest", "Basic Land — Forest");
        forest2.ManaAbility = ManaAbility.Fixed(ManaColor.Green);
        p1.Battlefield.Add(forest1);
        p1.Battlefield.Add(forest2);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest1.Id));
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest2.Id));
        p1.ManaPool[ManaColor.Green].Should().Be(2);

        // Undo last tap (forest2)
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        forest2.IsTapped.Should().BeFalse();
        p1.ManaPool[ManaColor.Green].Should().Be(1);

        // Undo next (forest1)
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        forest1.IsTapped.Should().BeFalse();
        p1.ManaPool[ManaColor.Green].Should().Be(0);
    }

    [Fact]
    public void Undo_EmptyHistory_ReturnsFalse()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var result = engine.UndoLastAction(p1.Id);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Undo_PlayCard_Rejected()
    {
        var engine = CreateEngine(out var state, out var p1, out _);
        var land = GameCard.Create("Forest", "Basic Land — Forest");
        p1.Hand.Add(land);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, land.Id));

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeFalse();
        state.GameLog.Should().Contain(l => l.Contains("Only land taps"));
    }

    [Fact]
    public async Task Undo_CastSpell_Rejected()
    {
        var engine = CreateEngine(out var state, out var p1, out var p2);

        // Set up a registered spell with mana
        var bolt = GameCard.Create("Lightning Bolt", "Instant");
        p1.Hand.Add(bolt);
        p1.ManaPool.Add(ManaColor.Red);

        // Put a creature on opponent's battlefield for targeting
        var target = GameCard.Create("Grizzly Bears", "Creature — Bear");
        target.TurnEnteredBattlefield = state.TurnNumber - 1;
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, bolt.Id));

        var result = engine.UndoLastAction(p1.Id);
        result.Should().BeFalse();
        state.GameLog.Should().Contain(l => l.Contains("Only land taps"));
    }

    [Fact]
    public async Task Undo_UntapCard_Rejected()
    {
        var engine = CreateEngine(out var state, out var p1, out _);
        var card = new GameCard { Name = "Forest", TypeLine = "Basic Land — Forest", IsTapped = true };
        p1.Battlefield.Add(card);

        await engine.ExecuteAction(GameAction.UntapCard(p1.Id, card.Id));

        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeFalse();
        state.GameLog.Should().Contain(l => l.Contains("Only land taps"));
    }

    [Fact]
    public async Task UndoTap_CardRemovedFromBattlefield_ReturnsFalse()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        forest.ManaAbility = ManaAbility.Fixed(ManaColor.Green);
        p1.Battlefield.Add(forest);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest.Id));

        // Remove card from battlefield externally
        p1.Battlefield.RemoveById(forest.Id);

        var result = engine.UndoLastAction(p1.Id);
        result.Should().BeFalse();
        p1.ActionHistory.Count.Should().Be(1, "history should not be consumed on failed undo");
    }

    [Fact]
    public async Task UndoTap_ChoiceAbility_RemovesMana()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var handler = (TestDecisionHandler)p1.DecisionHandler;
        var land = GameCard.Create("Karplusan Forest", "Land");
        p1.Battlefield.Add(land);
        handler.EnqueueManaColor(ManaColor.Green);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, land.Id));
        p1.ManaPool[ManaColor.Green].Should().Be(1);

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.ManaPool[ManaColor.Green].Should().Be(0, "chosen mana should be removed on undo");
    }

    [Fact]
    public async Task UndoTap_NoManaAbility_StillUntaps()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var creature = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        creature.TurnEnteredBattlefield = 0; // not summoning sick
        p1.Battlefield.Add(creature);
        // Pre-add some mana to ensure undo doesn't touch it
        p1.ManaPool.Add(ManaColor.Red, 2);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, creature.Id));

        engine.UndoLastAction(p1.Id).Should().BeTrue();
        creature.IsTapped.Should().BeFalse();
        p1.ManaPool[ManaColor.Red].Should().Be(2, "unrelated mana should not be affected");
    }

    [Fact]
    public async Task PendingManaTaps_ClearedOnCastSpell()
    {
        var engine = CreateEngine(out _, out var p1, out var p2);
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        mountain.ManaAbility = ManaAbility.Fixed(ManaColor.Red);
        p1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, mountain.Id));
        p1.PendingManaTaps.Should().Contain(mountain.Id);

        // Cast a spell that costs {R}
        var bolt = GameCard.Create("Lightning Bolt", "Instant");
        p1.Hand.Add(bolt);
        var target = GameCard.Create("Grizzly Bears", "Creature — Bear");
        target.TurnEnteredBattlefield = 0;
        p2.Battlefield.Add(target);

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, bolt.Id));

        p1.PendingManaTaps.Should().BeEmpty("pending taps should be cleared when mana is spent");
    }

    [Fact]
    public async Task PendingManaTaps_ClearedOnPlaySpell()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        mountain.ManaAbility = ManaAbility.Fixed(ManaColor.Red);
        p1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, mountain.Id));
        p1.PendingManaTaps.Should().Contain(mountain.Id);

        // Play a creature using PlayCard (which pays mana)
        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        p1.Hand.Add(goblin);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, goblin.Id));

        p1.PendingManaTaps.Should().BeEmpty("pending taps should be cleared when mana is spent via PlayCard");
    }

    [Fact]
    public async Task PendingManaTaps_TrackedOnTap()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        forest.ManaAbility = ManaAbility.Fixed(ManaColor.Green);
        p1.Battlefield.Add(forest);

        p1.PendingManaTaps.Should().BeEmpty();

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest.Id));

        p1.PendingManaTaps.Should().Contain(forest.Id);
    }

    [Fact]
    public async Task UndoTap_LandWithWildGrowth_RemovesBonusMana()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        p1.Battlefield.Add(forest);

        // Attach Wild Growth aura to the forest
        var wildGrowth = GameCard.Create("Wild Growth", "Enchantment — Aura");
        wildGrowth.AttachedTo = forest.Id;
        p1.Battlefield.Add(wildGrowth);

        // Tap forest — should produce G (land) + G (Wild Growth trigger)
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest.Id));
        p1.ManaPool[ManaColor.Green].Should().Be(2, "forest produces G + Wild Growth adds G");

        // Undo the tap — should remove ALL mana produced (land + bonus)
        var result = engine.UndoLastAction(p1.Id);

        result.Should().BeTrue();
        forest.IsTapped.Should().BeFalse();
        p1.ManaPool[ManaColor.Green].Should().Be(0, "both land mana and Wild Growth bonus should be removed on undo");
    }

    [Fact]
    public async Task UndoTap_Painland_RestoresLife()
    {
        var engine = CreateEngine(out _, out var p1, out _);
        var handler = (TestDecisionHandler)p1.DecisionHandler;
        var brushland = GameCard.Create("Brushland", "Land");
        p1.Battlefield.Add(brushland);
        handler.EnqueueManaColor(ManaColor.Green);

        var initialLife = p1.Life;
        await engine.ExecuteAction(GameAction.TapCard(p1.Id, brushland.Id));

        p1.Life.Should().Be(initialLife - 1, "painland should deal 1 damage for colored mana");
        p1.ManaPool[ManaColor.Green].Should().Be(1);

        // Undo should restore life
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        p1.Life.Should().Be(initialLife, "undo should restore the 1 damage from painland");
        p1.ManaPool[ManaColor.Green].Should().Be(0);
    }

    [Fact]
    public async Task UndoTap_DynamicMana_RemovesAllProducedMana()
    {
        var engine = CreateEngine(out _, out var p1, out _);

        // Serra's Sanctum produces W for each enchantment you control
        var sanctum = GameCard.Create("Serra's Sanctum", "Legendary Land");
        p1.Battlefield.Add(sanctum);

        // Add 3 enchantments so Sanctum produces 3 White
        for (int i = 0; i < 3; i++)
        {
            var ench = new GameCard { Name = $"Enchantment{i}", CardTypes = CardType.Enchantment };
            p1.Battlefield.Add(ench);
        }

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, sanctum.Id));
        p1.ManaPool[ManaColor.White].Should().Be(3, "Sanctum should produce 3 White with 3 enchantments");

        // Undo should remove all 3 White
        engine.UndoLastAction(p1.Id).Should().BeTrue();
        sanctum.IsTapped.Should().BeFalse();
        p1.ManaPool[ManaColor.White].Should().Be(0, "all dynamic mana should be removed on undo");
    }

    [Fact]
    public async Task Undo_LogsUntapMessage()
    {
        var engine = CreateEngine(out var state, out var p1, out _);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        forest.ManaAbility = ManaAbility.Fixed(ManaColor.Green);
        p1.Battlefield.Add(forest);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, forest.Id));
        state.GameLog.Clear();

        engine.UndoLastAction(p1.Id);

        state.GameLog.Should().Contain(l => l.Contains("untaps") && l.Contains("Forest"));
    }
}
