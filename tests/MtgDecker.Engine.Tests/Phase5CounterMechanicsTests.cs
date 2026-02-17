using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase5CounterMechanicsTests
{
    private static (GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) Setup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2, h1, h2);
    }

    // === Gemstone Mine: AddCountersEffect ===

    [Fact]
    public async Task AddCountersEffect_PlacesCountersOnSource()
    {
        var (state, p1, _, h1, _) = Setup();
        var mine = new GameCard { Name = "Gemstone Mine", CardTypes = CardType.Land };
        p1.Battlefield.Add(mine);
        var context = new EffectContext(state, p1, mine, h1);

        var effect = new AddCountersEffect(CounterType.Mining, 3);
        await effect.Execute(context);

        mine.GetCounters(CounterType.Mining).Should().Be(3);
    }

    // === Gemstone Mine: ManaAbility.DepletionChoice ===

    [Fact]
    public void DepletionChoice_HasRemovesCounterOnTap()
    {
        var ability = ManaAbility.DepletionChoice(CounterType.Mining,
            ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green);

        ability.Type.Should().Be(ManaAbilityType.Choice);
        ability.RemovesCounterOnTap.Should().Be(CounterType.Mining);
        ability.ChoiceColors.Should().HaveCount(5);
    }

    // === Gemstone Mine: integration via GameEngine ===

    [Fact]
    public async Task GemstoneMine_TapRemovesCounter_SacrificesWhenEmpty()
    {
        var (state, p1, p2, h1, h2) = Setup();

        // Create Gemstone Mine with 3 mining counters
        var mine = GameCard.Create("Gemstone Mine", "Land", null);
        mine.AddCounters(CounterType.Mining, 3);
        p1.Battlefield.Add(mine);
        mine.TurnEnteredBattlefield = 0;

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        // Tap: choose blue, counter goes from 3 → 2
        h1.EnqueueManaColor(ManaColor.Blue);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, mine.Id));

        mine.GetCounters(CounterType.Mining).Should().Be(2);
        p1.ManaPool.Total.Should().Be(1);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == mine.Id, "still on battlefield with 2 counters");
    }

    [Fact]
    public async Task GemstoneMine_SacrificesOnLastCounter()
    {
        var (state, p1, _, h1, _) = Setup();

        var mine = GameCard.Create("Gemstone Mine", "Land", null);
        mine.AddCounters(CounterType.Mining, 1); // Only 1 counter left
        p1.Battlefield.Add(mine);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        h1.EnqueueManaColor(ManaColor.Red);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, mine.Id));

        mine.GetCounters(CounterType.Mining).Should().Be(0);
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == mine.Id, "sacrificed after last counter");
        p1.Graveyard.Cards.Should().Contain(c => c.Id == mine.Id);
        p1.ManaPool.Total.Should().Be(1, "still produced mana before being sacrificed");
    }

    // === Gemstone Mine: CardDefinition verification ===

    [Fact]
    public void GemstoneMine_CardDef_HasDepletionManaAbility()
    {
        CardDefinitions.TryGet("Gemstone Mine", out var def);

        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Choice);
        def.ManaAbility.RemovesCounterOnTap.Should().Be(CounterType.Mining);
        def.ManaAbility.ChoiceColors.Should().HaveCount(5);
    }

    [Fact]
    public void GemstoneMine_CardDef_HasETBCounterTrigger()
    {
        CardDefinitions.TryGet("Gemstone Mine", out var def);

        def!.Triggers.Should().ContainSingle(t =>
            t.Event == GameEvent.EnterBattlefield
            && t.Effect is AddCountersEffect);

        var etb = (AddCountersEffect)def.Triggers.First().Effect;
        etb.CounterType.Should().Be(CounterType.Mining);
        etb.Count.Should().Be(3);
    }

    // === Priest of Titania: DynamicAddManaEffect ===

    [Fact]
    public async Task DynamicAddManaEffect_AddsCountBasedMana()
    {
        var (state, p1, _, h1, _) = Setup();

        // Put 3 Elves on the battlefield
        p1.Battlefield.Add(new GameCard { Name = "Llanowar Elves", Subtypes = ["Elf", "Druid"] });
        p1.Battlefield.Add(new GameCard { Name = "Fyndhorn Elves", Subtypes = ["Elf", "Druid"] });
        var priest = new GameCard { Name = "Priest of Titania", Subtypes = ["Elf", "Druid"] };
        p1.Battlefield.Add(priest);

        var context = new EffectContext(state, p1, priest, h1);
        var effect = new DynamicAddManaEffect(ManaColor.Green,
            p => p.Battlefield.Cards.Count(c => c.Subtypes.Contains("Elf", StringComparer.OrdinalIgnoreCase)));
        await effect.Execute(context);

        p1.ManaPool.Total.Should().Be(3, "3 Elves on battlefield");
    }

    [Fact]
    public async Task DynamicAddManaEffect_ZeroElves_ProducesNoMana()
    {
        var (state, p1, _, h1, _) = Setup();

        // Priest alone but we use a lambda that counts NON-self elves for test purposes
        // Actually, Priest counts itself too, so let's test with no creatures at all
        var source = new GameCard { Name = "Test Card" };
        p1.Battlefield.Add(source);
        var context = new EffectContext(state, p1, source, h1);
        var effect = new DynamicAddManaEffect(ManaColor.Green,
            p => p.Battlefield.Cards.Count(c => c.Subtypes.Contains("Elf")));
        await effect.Execute(context);

        p1.ManaPool.Total.Should().Be(0, "no Elves on battlefield");
    }

    // === Priest of Titania: CardDefinition verification ===

    [Fact]
    public void PriestOfTitania_CardDef_HasDynamicManaAbility()
    {
        CardDefinitions.TryGet("Priest of Titania", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Effect.Should().BeOfType<DynamicAddManaEffect>();
    }

    [Fact]
    public async Task PriestOfTitania_IntegrationTest_TapsForElfCount()
    {
        var (state, p1, _, h1, _) = Setup();

        var priest = new GameCard { Name = "Priest of Titania", Subtypes = ["Elf", "Druid"],
            CardTypes = CardType.Creature };
        p1.Battlefield.Add(priest);
        priest.TurnEnteredBattlefield = 0; // not summoning sick

        var elf2 = new GameCard { Name = "Llanowar Elves", Subtypes = ["Elf", "Druid"],
            CardTypes = CardType.Creature };
        p1.Battlefield.Add(elf2);

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;
        state.TurnNumber = 2;
        var engine = new GameEngine(state);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, priest.Id));

        // Ability goes on stack — resolve it
        state.Stack.Should().HaveCount(1);
        await engine.ResolveAllTriggersAsync();

        p1.ManaPool.Total.Should().Be(2, "2 Elves on battlefield (Priest + Llanowar)");
    }

    // === Graveborn Muse: DynamicDrawAndLoseLifeEffect ===

    [Fact]
    public async Task DynamicDrawAndLoseLife_DrawsAndLosesPerZombieCount()
    {
        var (state, p1, _, h1, _) = Setup();

        // Put 3 Zombies on battlefield
        p1.Battlefield.Add(new GameCard { Name = "Zombie A", Subtypes = ["Zombie"] });
        p1.Battlefield.Add(new GameCard { Name = "Zombie B", Subtypes = ["Zombie"] });
        var muse = new GameCard { Name = "Graveborn Muse", Subtypes = ["Zombie", "Spirit"] };
        p1.Battlefield.Add(muse);

        // Stock library with cards to draw
        for (int i = 0; i < 5; i++)
            p1.Library.AddToTop(new GameCard { Name = $"Card {i}" });

        var context = new EffectContext(state, p1, muse, h1);
        var effect = new DynamicDrawAndLoseLifeEffect(
            p => p.Battlefield.Cards.Count(c => c.Subtypes.Contains("Zombie", StringComparer.OrdinalIgnoreCase)));
        await effect.Execute(context);

        p1.Hand.Count.Should().Be(3, "drew 3 cards for 3 Zombies");
        p1.Life.Should().Be(17, "lost 3 life for 3 Zombies (20 - 3)");
    }

    [Fact]
    public async Task DynamicDrawAndLoseLife_OneZombie_DrawsOneLosesOne()
    {
        var (state, p1, _, h1, _) = Setup();

        var muse = new GameCard { Name = "Graveborn Muse", Subtypes = ["Zombie", "Spirit"] };
        p1.Battlefield.Add(muse);

        p1.Library.AddToTop(new GameCard { Name = "Card 1" });

        var context = new EffectContext(state, p1, muse, h1);
        var effect = new DynamicDrawAndLoseLifeEffect(
            p => p.Battlefield.Cards.Count(c => c.Subtypes.Contains("Zombie", StringComparer.OrdinalIgnoreCase)));
        await effect.Execute(context);

        p1.Hand.Count.Should().Be(1, "drew 1 card for 1 Zombie");
        p1.Life.Should().Be(19, "lost 1 life for 1 Zombie");
    }

    [Fact]
    public async Task DynamicDrawAndLoseLife_NoZombies_DoesNothing()
    {
        var (state, p1, _, h1, _) = Setup();

        var source = new GameCard { Name = "Some Card" };
        p1.Battlefield.Add(source);
        p1.Library.AddToTop(new GameCard { Name = "Card 1" });

        var context = new EffectContext(state, p1, source, h1);
        var effect = new DynamicDrawAndLoseLifeEffect(
            p => p.Battlefield.Cards.Count(c => c.Subtypes.Contains("Zombie")));
        await effect.Execute(context);

        p1.Hand.Count.Should().Be(0, "no Zombies, no draw");
        p1.Life.Should().Be(20, "no life loss");
    }

    // === Graveborn Muse: CardDefinition verification ===

    [Fact]
    public void GravebornMuse_CardDef_HasDynamicDrawTrigger()
    {
        CardDefinitions.TryGet("Graveborn Muse", out var def);

        def!.Triggers.Should().ContainSingle(t =>
            t.Event == GameEvent.Upkeep
            && t.Effect is DynamicDrawAndLoseLifeEffect);
    }
}
