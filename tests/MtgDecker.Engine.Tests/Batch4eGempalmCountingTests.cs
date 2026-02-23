using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class GempalmIncineratorCountingTests
{
    private static (GameState state, Player p1, Player p2, TestDecisionHandler h1) CreateGameState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", h1);
        var p2 = new Player(Guid.NewGuid(), "Bob", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2, h1);
    }

    [Fact]
    public async Task GempalmIncinerator_CountsOpponentGoblins()
    {
        // Arrange
        var (state, p1, p2, h1) = CreateGameState();

        // P1 has 1 Goblin on the field
        var p1Goblin = GameCard.Create("Goblin Lackey");
        p1.Battlefield.Add(p1Goblin);

        // P2 has 2 Goblins on the field
        var p2Goblin1 = GameCard.Create("Mogg Fanatic");
        var p2Goblin2 = GameCard.Create("Goblin Piledriver");
        p2.Battlefield.Add(p2Goblin1);
        p2.Battlefield.Add(p2Goblin2);

        // Create a target creature to deal damage to
        var target = new GameCard { Name = "Tarmogoyf", CardTypes = CardType.Creature, BasePower = 4, BaseToughness = 5 };
        p2.Battlefield.Add(target);

        var source = GameCard.Create("Gempalm Incinerator");
        var effect = new GempalmIncineratorEffect();
        var context = new EffectContext(state, p1, source, h1);

        h1.EnqueueCardChoice(target.Id);

        // Act
        await effect.Execute(context);

        // Assert — should count ALL 3 goblins (1 from p1 + 2 from p2), not just p1's 1
        target.DamageMarked.Should().Be(3,
            "Gempalm Incinerator should count Goblins from ALL players (1 + 2 = 3)");
    }

    [Fact]
    public async Task GempalmIncinerator_CountsOnlyControllerGoblins_BeforeFix()
    {
        // This test documents the old (incorrect) behavior for reference.
        // After fix, damage should be 3 not 1 — see test above.
        var (state, p1, p2, h1) = CreateGameState();

        // P1 has no goblins
        // P2 has 2 Goblins
        var p2Goblin1 = GameCard.Create("Mogg Fanatic");
        var p2Goblin2 = GameCard.Create("Goblin Piledriver");
        p2.Battlefield.Add(p2Goblin1);
        p2.Battlefield.Add(p2Goblin2);

        var target = new GameCard { Name = "Llanowar Elves", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(target);

        var source = GameCard.Create("Gempalm Incinerator");
        var effect = new GempalmIncineratorEffect();
        var context = new EffectContext(state, p1, source, h1);

        h1.EnqueueCardChoice(target.Id);

        // Act
        await effect.Execute(context);

        // Assert — with no controller goblins but opponent has 2,
        // after fix it should count the opponent's goblins (2 total)
        target.DamageMarked.Should().Be(2,
            "Gempalm Incinerator should count opponent's Goblins too");
    }

    [Fact]
    public async Task PriestOfTitania_CountsAllElves_BothPlayers()
    {
        // Arrange
        var (state, p1, p2, h1) = CreateGameState();

        // P1 has 2 Elves (including Priest)
        var priest = new GameCard { Name = "Priest of Titania", Subtypes = ["Elf", "Druid"] };
        p1.Battlefield.Add(priest);
        p1.Battlefield.Add(new GameCard { Name = "Llanowar Elves", Subtypes = ["Elf", "Druid"] });

        // P2 has 1 Elf
        p2.Battlefield.Add(new GameCard { Name = "Fyndhorn Elves", Subtypes = ["Elf", "Druid"] });

        // Use the state-aware overload
        var effect = new DynamicAddManaEffect(ManaColor.Green,
            s => s.Player1.Battlefield.Cards
                .Concat(s.Player2.Battlefield.Cards)
                .Count(c => c.Subtypes.Contains("Elf", StringComparer.OrdinalIgnoreCase)));
        var context = new EffectContext(state, p1, priest, h1);

        // Act
        await effect.Execute(context);

        // Assert — should count ALL 3 elves (2 from p1 + 1 from p2)
        p1.ManaPool.Total.Should().Be(3,
            "Priest of Titania should count Elves from ALL players (2 + 1 = 3)");
    }

    [Fact]
    public void PriestOfTitania_CardDef_UsesStateCountFunc()
    {
        CardDefinitions.TryGet("Priest of Titania", out var def);
        var effect = def!.ActivatedAbilities[0].Effect as DynamicAddManaEffect;
        effect.Should().NotBeNull();
        effect!.StateCountFunc.Should().NotBeNull(
            "Priest of Titania should use the state-aware counting function to count all Elves");
    }
}
