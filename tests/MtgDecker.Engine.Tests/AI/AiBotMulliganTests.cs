using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotMulliganTests
{
    private readonly AiBotDecisionHandler _bot = new();

    private IReadOnlyList<GameCard> MakeHand(int lands, int spells)
    {
        var hand = new List<GameCard>();
        for (int i = 0; i < lands; i++)
            hand.Add(new GameCard { Name = "Mountain", CardTypes = CardType.Land });
        for (int i = 0; i < spells; i++)
            hand.Add(new GameCard { Name = "Goblin", CardTypes = CardType.Creature });
        return hand;
    }

    [Theory]
    [InlineData(2, 5, 0, MulliganDecision.Keep)]
    [InlineData(3, 4, 0, MulliganDecision.Keep)]
    [InlineData(5, 2, 0, MulliganDecision.Keep)]
    [InlineData(0, 7, 0, MulliganDecision.Mulligan)]
    [InlineData(1, 6, 0, MulliganDecision.Mulligan)]
    [InlineData(6, 1, 0, MulliganDecision.Mulligan)]
    [InlineData(7, 0, 0, MulliganDecision.Mulligan)]
    public async Task MulliganDecision_SevenCardHand(int lands, int spells, int mulliganCount, MulliganDecision expected)
    {
        var hand = MakeHand(lands, spells);
        var result = await _bot.GetMulliganDecision(hand, mulliganCount);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(2, 4, 1, MulliganDecision.Keep)]
    [InlineData(0, 6, 1, MulliganDecision.Mulligan)]
    [InlineData(5, 1, 1, MulliganDecision.Mulligan)]
    public async Task MulliganDecision_SixCardHand(int lands, int spells, int mulliganCount, MulliganDecision expected)
    {
        var hand = MakeHand(lands, spells);
        var result = await _bot.GetMulliganDecision(hand, mulliganCount);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task MulliganDecision_FourOrFewer_AlwaysKeeps()
    {
        var hand = MakeHand(0, 4);
        var result = await _bot.GetMulliganDecision(hand, 3);
        result.Should().Be(MulliganDecision.Keep);
    }

    [Fact]
    public async Task ChooseCardsToBottom_ReturnsCorrectCount()
    {
        var hand = MakeHand(4, 3);
        var result = await _bot.ChooseCardsToBottom(hand, 2);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ChooseCardsToBottom_BottomsExcessLandsFirst()
    {
        // 5 lands + 2 spells, bottom 2 should prefer excess lands
        var hand = MakeHand(5, 2);
        var result = await _bot.ChooseCardsToBottom(hand, 2);
        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.IsLand);
    }

    [Fact]
    public async Task ChooseCardsToBottom_BottomsCheapestSpellsWhenNoExcessLands()
    {
        // 3 lands + 4 spells with different costs, bottom 1
        var hand = new List<GameCard>
        {
            new() { Name = "Mountain", CardTypes = CardType.Land },
            new() { Name = "Mountain", CardTypes = CardType.Land },
            new() { Name = "Mountain", CardTypes = CardType.Land },
            new() { Name = "Expensive Creature", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{4}{R}{R}") },
            new() { Name = "Medium Creature", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{2}{R}") },
            new() { Name = "Cheap Creature", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") },
            new() { Name = "Mid Creature", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{1}{R}") },
        };
        var result = await _bot.ChooseCardsToBottom(hand, 1);
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Cheap Creature");
    }

    [Fact]
    public async Task ChooseManaColor_ReturnsValidOption()
    {
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };
        var result = await _bot.ChooseManaColor(options);
        options.Should().Contain(result);
    }

    [Fact]
    public async Task ChooseManaColor_PrefersColoredOverColorless()
    {
        var options = new List<ManaColor> { ManaColor.Colorless, ManaColor.Red };
        var result = await _bot.ChooseManaColor(options);
        result.Should().Be(ManaColor.Red);
    }

    [Fact]
    public async Task ChooseManaColor_ReturnsColorlessWhenOnlyOption()
    {
        var options = new List<ManaColor> { ManaColor.Colorless };
        var result = await _bot.ChooseManaColor(options);
        result.Should().Be(ManaColor.Colorless);
    }

    [Fact]
    public async Task RevealCards_AutoAcknowledges()
    {
        // Should complete without throwing
        await _bot.RevealCards([], [], "test");
    }

    [Fact]
    public async Task ChooseCard_ReturnsFromOptions()
    {
        var cards = new List<GameCard>
        {
            new() { Name = "Goblin Matron", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{2}{R}") },
            new() { Name = "Goblin Lackey", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") }
        };
        var result = await _bot.ChooseCard(cards, "Choose a Goblin");
        result.Should().NotBeNull();
        cards.Select(c => c.Id).Should().Contain(result!.Value);
    }

    [Fact]
    public async Task ChooseCard_PrefersHigherCmc()
    {
        var cards = new List<GameCard>
        {
            new() { Name = "Goblin Lackey", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{R}") },
            new() { Name = "Goblin Matron", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{2}{R}") },
            new() { Name = "Goblin Chieftain", CardTypes = CardType.Creature, ManaCost = ManaCost.Parse("{1}{R}{R}") }
        };
        var result = await _bot.ChooseCard(cards, "Choose a Goblin");
        result.Should().NotBeNull();
        // Should prefer highest CMC (Matron at 3 or Chieftain at 3)
        var chosen = cards.First(c => c.Id == result!.Value);
        chosen.ManaCost!.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public async Task ChooseCard_ReturnsNullWhenOptionalAndEmpty()
    {
        var result = await _bot.ChooseCard([], "Choose a card", optional: true);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAction_NonMainPhase_ReturnsPass()
    {
        // Default phase is Untap, so GetAction should pass
        var p1 = new Player(Guid.NewGuid(), "Bot", _bot);
        var p2 = new Player(Guid.NewGuid(), "Opponent", _bot);
        var state = new GameState(p1, p2);
        var result = await _bot.GetAction(state, p1.Id);
        result.Type.Should().Be(ActionType.PassPriority);
    }

    [Fact]
    public async Task ChooseAttackers_ReturnsEmpty_Stub()
    {
        var result = await _bot.ChooseAttackers([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChooseBlockers_ReturnsEmpty_Stub()
    {
        var result = await _bot.ChooseBlockers([], []);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OrderBlockers_ReturnsInOrder_Stub()
    {
        var b1 = new GameCard { Name = "Blocker 1", CardTypes = CardType.Creature };
        var b2 = new GameCard { Name = "Blocker 2", CardTypes = CardType.Creature };
        var blockers = new List<GameCard> { b1, b2 };
        var result = await _bot.OrderBlockers(Guid.NewGuid(), blockers);
        result.Should().HaveCount(2);
        result.Should().Contain(b1.Id);
        result.Should().Contain(b2.Id);
    }
}
