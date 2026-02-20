using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class EmrakulMechanicsTests
{
    [Fact]
    public void MoveToGraveyardWithReplacement_ShufflesGraveyardIntoLibrary()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{15}"), null, 15, 15, CardType.Creature)
        {
            Name = "TestEmrakul",
            ShuffleGraveyardOnDeath = true,
        });

        var emrakul = new GameCard { Name = "TestEmrakul", CardTypes = CardType.Creature };
        state.Player1.Graveyard.Add(GameCard.Create("Other Card"));

        engine.MoveToGraveyardWithReplacement(emrakul, state.Player1);

        state.Player1.Graveyard.Cards.Should().BeEmpty();
        state.Player1.Library.Cards.Should().Contain(c => c.Name == "TestEmrakul");
        state.Player1.Library.Cards.Should().Contain(c => c.Name == "Other Card");

        CardDefinitions.Unregister("TestEmrakul");
    }

    [Fact]
    public void MoveToGraveyardWithReplacement_NormalCard_GoesToGraveyard()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var bear = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        engine.MoveToGraveyardWithReplacement(bear, state.Player1);

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Bear");
    }

    [Fact]
    public void CanTargetWithSpell_ProtectionFromColored_BlocksColoredSpell()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var emrakul = new GameCard
        {
            Name = "Emrakul",
            CardTypes = CardType.Creature,
        };
        emrakul.ActiveKeywords.Add(Keyword.ProtectionFromColoredSpells);

        var swords = new GameCard { Name = "Swords", ManaCost = ManaCost.Parse("{W}") };

        engine.CanTargetWithSpell(emrakul, swords).Should().BeFalse();
    }

    [Fact]
    public void CanTargetWithSpell_ProtectionFromColored_AllowsColorlessSpell()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);

        var emrakul = new GameCard
        {
            Name = "Emrakul",
            CardTypes = CardType.Creature,
        };
        emrakul.ActiveKeywords.Add(Keyword.ProtectionFromColoredSpells);

        var colorless = new GameCard { Name = "Colorless", ManaCost = ManaCost.Parse("{7}") };

        engine.CanTargetWithSpell(emrakul, colorless).Should().BeTrue();
    }

    [Fact]
    public void ManaCost_IsColored_TrueForColoredCost()
    {
        ManaCost.Parse("{1}{R}").IsColored.Should().BeTrue();
        ManaCost.Parse("{W}").IsColored.Should().BeTrue();
        ManaCost.Parse("{U}{B}").IsColored.Should().BeTrue();
    }

    [Fact]
    public void ManaCost_IsColored_FalseForColorlessCost()
    {
        ManaCost.Parse("{5}").IsColored.Should().BeFalse();
        ManaCost.Parse("{0}").IsColored.Should().BeFalse();
    }
}
