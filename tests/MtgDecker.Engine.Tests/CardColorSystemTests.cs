using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers.Effects;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

/// <summary>
/// Tests for the card color system: Colors property on GameCard, token colors,
/// color-based effects using Colors instead of ManaCost.
/// </summary>
public class CardColorSystemTests
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

    private static (GameEngine engine, GameState state, Player p1, Player p2) CreateEngineState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2);
    }

    private static StackObject CreateSpell(string name, Guid controllerId, List<TargetInfo> targets)
    {
        var card = GameCard.Create(name);
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(), targets, 0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Colors property initialization
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Colors_InitializedFromManaCost_WhenSetDirectly()
    {
        var card = new GameCard { ManaCost = ManaCost.Parse("{1}{R}") };
        card.Colors.Should().Contain(ManaColor.Red);
        card.Colors.Should().HaveCount(1);
    }

    [Fact]
    public void Colors_InitializedFromManaCost_MultiColor()
    {
        var card = new GameCard { ManaCost = ManaCost.Parse("{W}{U}") };
        card.Colors.Should().Contain(ManaColor.White);
        card.Colors.Should().Contain(ManaColor.Blue);
        card.Colors.Should().HaveCount(2);
    }

    [Fact]
    public void Colors_Empty_ForColorlessCard()
    {
        var card = new GameCard { ManaCost = ManaCost.Parse("{3}") };
        card.Colors.Should().BeEmpty();
    }

    [Fact]
    public void Colors_Empty_ForLand()
    {
        var card = GameCard.Create("Forest", "Basic Land — Forest");
        card.Colors.Should().BeEmpty();
    }

    [Fact]
    public void Colors_InitializedFromCreate()
    {
        var card = GameCard.Create("Lightning Bolt");
        card.Colors.Should().Contain(ManaColor.Red);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Token colors
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateTokensEffect_SetsTokenColors()
    {
        var (state, p1, _) = CreateGameState();
        var source = new GameCard { Name = "Test" };
        var ctx = new MtgDecker.Engine.Triggers.EffectContext(state, p1, source, p1.DecisionHandler);

        var effect = new CreateTokensEffect("Goblin", 1, 1, CardType.Creature,
            ["Goblin"], count: 1, tokenColors: [ManaColor.Red]);
        await effect.Execute(ctx);

        var token = p1.Battlefield.Cards.First();
        token.Colors.Should().Contain(ManaColor.Red);
    }

    [Fact]
    public async Task CreateTokensEffect_NoColors_WhenNotSpecified()
    {
        var (state, p1, _) = CreateGameState();
        var source = new GameCard { Name = "Test" };
        var ctx = new MtgDecker.Engine.Triggers.EffectContext(state, p1, source, p1.DecisionHandler);

        var effect = new CreateTokensEffect("Spirit", 1, 1, CardType.Creature, ["Spirit"]);
        await effect.Execute(ctx);

        var token = p1.Battlefield.Cards.First();
        token.Colors.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // White token gets Crusade buff
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Crusade_Buffs_WhiteToken()
    {
        var (engine, state, p1, _) = CreateEngineState();

        var crusade = GameCard.Create("Crusade");
        p1.Battlefield.Add(crusade);

        // Create a white token
        var token = new GameCard
        {
            Name = "Soldier",
            BasePower = 1,
            BaseToughness = 1,
            CardTypes = CardType.Creature,
            IsToken = true,
            Colors = { ManaColor.White },
        };
        p1.Battlefield.Add(token);

        engine.RecalculateState();

        token.Power.Should().Be(2); // 1 + 1 from Crusade
        token.Toughness.Should().Be(2); // 1 + 1 from Crusade
    }

    [Fact]
    public async Task Crusade_DoesNotBuff_NonWhiteToken()
    {
        var (engine, state, p1, _) = CreateEngineState();

        var crusade = GameCard.Create("Crusade");
        p1.Battlefield.Add(crusade);

        var token = new GameCard
        {
            Name = "Goblin",
            BasePower = 1,
            BaseToughness = 1,
            CardTypes = CardType.Creature,
            IsToken = true,
            Colors = { ManaColor.Red },
        };
        p1.Battlefield.Add(token);

        engine.RecalculateState();

        token.Power.Should().Be(1); // No buff
        token.Toughness.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Green token destroyed by Perish
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Perish_Destroys_GreenToken()
    {
        var (state, p1, p2) = CreateGameState();

        var greenToken = new GameCard
        {
            Name = "Bear",
            BasePower = 2,
            BaseToughness = 2,
            CardTypes = CardType.Creature,
            IsToken = true,
            Colors = { ManaColor.Green },
        };
        p2.Battlefield.Add(greenToken);

        var spell = CreateSpell("Perish", p1.Id, []);
        CardDefinitions.TryGet("Perish", out var def);
        def!.Effect!.Resolve(state, spell);

        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Bear");
    }

    [Fact]
    public void Perish_DoesNotDestroy_NonGreenToken()
    {
        var (state, p1, p2) = CreateGameState();

        var whiteToken = new GameCard
        {
            Name = "Soldier",
            BasePower = 1,
            BaseToughness = 1,
            CardTypes = CardType.Creature,
            IsToken = true,
            Colors = { ManaColor.White },
        };
        p2.Battlefield.Add(whiteToken);

        var spell = CreateSpell("Perish", p1.Id, []);
        CardDefinitions.TryGet("Perish", out var def);
        def!.Effect!.Resolve(state, spell);

        p2.Battlefield.Cards.Should().Contain(c => c.Name == "Soldier");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dystopia uses Colors for green/white check
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Dystopia_SacrificesGreenToken()
    {
        var (state, p1, _) = CreateGameState();
        state.ActivePlayer = p1;

        var greenToken = new GameCard
        {
            Name = "Elephant",
            BasePower = 3,
            BaseToughness = 3,
            CardTypes = CardType.Creature,
            IsToken = true,
            Colors = { ManaColor.Green },
        };
        p1.Battlefield.Add(greenToken);

        var dystopia = GameCard.Create("Dystopia");
        var ctx = new MtgDecker.Engine.Triggers.EffectContext(state, p1, dystopia, p1.DecisionHandler);
        var effect = new DystopiaSacrificeEffect();
        await effect.Execute(ctx);

        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Elephant");
    }

    // ═══════════════════════════════════════════════════════════════════
    // NonBlackCreature target filter uses Colors
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void NonBlackCreature_ExcludesBlackTokens()
    {
        var blackToken = new GameCard
        {
            Name = "Zombie",
            CardTypes = CardType.Creature,
            IsToken = true,
            Colors = { ManaColor.Black },
        };

        var filter = TargetFilter.NonBlackCreature();
        filter.IsLegal(blackToken, ZoneType.Battlefield).Should().BeFalse();
    }

    [Fact]
    public void NonBlackCreature_IncludesColorlessToken()
    {
        var colorlessToken = new GameCard
        {
            Name = "Myr",
            CardTypes = CardType.Creature,
            IsToken = true,
        };

        var filter = TargetFilter.NonBlackCreature();
        filter.IsLegal(colorlessToken, ZoneType.Battlefield).Should().BeTrue();
    }
}
