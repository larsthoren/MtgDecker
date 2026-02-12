using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DynamicManaTests
{
    [Fact]
    public void ManaAbility_Dynamic_Creates_Correct_Type()
    {
        var ability = ManaAbility.Dynamic(ManaColor.White, p => p.Battlefield.Cards.Count);
        ability.Type.Should().Be(ManaAbilityType.Dynamic);
        ability.DynamicColor.Should().Be(ManaColor.White);
        ability.CountFunc.Should().NotBeNull();
    }

    [Fact]
    public async Task SerrasSanctum_Taps_For_White_Per_Enchantment()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sanctum = GameCard.Create("Serra's Sanctum");
        p1.Battlefield.Add(sanctum);

        p1.Battlefield.Add(new GameCard { Name = "Enchantment1", CardTypes = CardType.Enchantment });
        p1.Battlefield.Add(new GameCard { Name = "Enchantment2", CardTypes = CardType.Enchantment });
        p1.Battlefield.Add(new GameCard { Name = "Enchantment3", CardTypes = CardType.Enchantment });

        var action = GameAction.TapCard(p1.Id, sanctum.Id);
        await engine.ExecuteAction(action);

        p1.ManaPool.Available[ManaColor.White].Should().Be(3);
    }

    [Fact]
    public async Task SerrasSanctum_Zero_Enchantments_Produces_No_Mana()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var sanctum = GameCard.Create("Serra's Sanctum");
        p1.Battlefield.Add(sanctum);

        var action = GameAction.TapCard(p1.Id, sanctum.Id);
        await engine.ExecuteAction(action);

        p1.ManaPool.Available.GetValueOrDefault(ManaColor.White).Should().Be(0);
    }
}
