using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ActivateAbilityEngineTests
{
    private (GameEngine engine, GameState state, Player p1, Player p2, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", h1);
        var p2 = new Player(Guid.NewGuid(), "Player 2", h2);

        // Add library cards to prevent deck-out
        for (int i = 0; i < 20; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, h1, h2);
    }

    // === Mogg Fanatic: sacrifice self, deal 1 damage ===

    [Fact]
    public async Task MoggFanatic_SacrificeSelf_DealsDamageToCreature()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var fanatic = GameCard.Create("Mogg Fanatic");
        p1.Battlefield.Add(fanatic);

        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, fanatic.Id, targetId: target.Id);
        await engine.ExecuteAction(action);

        // Fanatic should be sacrificed (in graveyard)
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == fanatic.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == fanatic.Id);

        // Target should have 1 damage
        target.DamageMarked.Should().Be(1);
    }

    [Fact]
    public async Task MoggFanatic_SacrificeSelf_DealsDamageToPlayer()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var fanatic = GameCard.Create("Mogg Fanatic");
        p1.Battlefield.Add(fanatic);

        var action = GameAction.ActivateAbility(p1.Id, fanatic.Id, targetPlayerId: p2.Id);
        await engine.ExecuteAction(action);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == fanatic.Id);
        p2.Life.Should().Be(19);
    }

    // === Goblin Sharpshooter: tap self, deal 1 damage ===

    [Fact]
    public async Task GoblinSharpshooter_TapSelf_DealsDamageToCreature()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var sharpshooter = GameCard.Create("Goblin Sharpshooter");
        p1.Battlefield.Add(sharpshooter);

        // Use a creature with toughness > 1 so it survives and doesn't trigger untap
        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 3 };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, sharpshooter.Id, targetId: target.Id);
        await engine.ExecuteAction(action);

        sharpshooter.IsTapped.Should().BeTrue();
        target.DamageMarked.Should().Be(1);
    }

    [Fact]
    public async Task GoblinSharpshooter_AlreadyTapped_CannotActivate()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var sharpshooter = GameCard.Create("Goblin Sharpshooter");
        sharpshooter.IsTapped = true;
        p1.Battlefield.Add(sharpshooter);

        var target = new GameCard { Name = "Elf", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, sharpshooter.Id, targetId: target.Id);
        await engine.ExecuteAction(action);

        target.DamageMarked.Should().Be(0);
        state.GameLog.Should().Contain(l => l.Contains("tapped"));
    }

    // === Skirk Prospector: sacrifice a Goblin, add {R} ===

    [Fact]
    public async Task SkirkProspector_SacrificeGoblin_AddsMana()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        var prospector = GameCard.Create("Skirk Prospector");
        p1.Battlefield.Add(prospector);

        var goblinToken = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], IsToken = true };
        p1.Battlefield.Add(goblinToken);

        // Choose the goblin token as sacrifice target
        h1.EnqueueCardChoice(goblinToken.Id);

        var action = GameAction.ActivateAbility(p1.Id, prospector.Id);
        await engine.ExecuteAction(action);

        // Token should be sacrificed
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == goblinToken.Id);
        // Mana should be added
        p1.ManaPool[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task SkirkProspector_CanSacrificeItself()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        var prospector = GameCard.Create("Skirk Prospector");
        p1.Battlefield.Add(prospector);

        // Prospector is itself a Goblin, so it can sacrifice itself
        h1.EnqueueCardChoice(prospector.Id);

        var action = GameAction.ActivateAbility(p1.Id, prospector.Id);
        await engine.ExecuteAction(action);

        p1.ManaPool[ManaColor.Red].Should().Be(1);
        p1.Battlefield.Cards.Should().NotContain(c => c.Id == prospector.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == prospector.Id);
    }

    [Fact]
    public async Task SiegeGangCommander_CanSacrificeItself()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var sgc = GameCard.Create("Siege-Gang Commander");
        p1.Battlefield.Add(sgc);

        p1.ManaPool.Add(ManaColor.Red, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // SGC is the only Goblin — sacrifice itself
        h1.EnqueueCardChoice(sgc.Id);

        var action = GameAction.ActivateAbility(p1.Id, sgc.Id, targetPlayerId: p2.Id);
        await engine.ExecuteAction(action);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == sgc.Id);
        p2.Life.Should().Be(18);
    }

    // === Siege-Gang Commander: sacrifice a Goblin + pay {1}{R}, deal 2 damage ===

    [Fact]
    public async Task SiegeGangCommander_SacrificeGoblinAndPayMana_DealsDamage()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var sgc = GameCard.Create("Siege-Gang Commander");
        p1.Battlefield.Add(sgc);

        var goblinToken = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], IsToken = true };
        p1.Battlefield.Add(goblinToken);

        // Add mana: 1R + 1 generic
        p1.ManaPool.Add(ManaColor.Red, 1);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Choose goblin token as sacrifice
        h1.EnqueueCardChoice(goblinToken.Id);

        // Use a creature with toughness > 2 so it survives the damage and we can verify DamageMarked
        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 3 };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, sgc.Id, targetId: target.Id);
        await engine.ExecuteAction(action);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == goblinToken.Id);
        target.DamageMarked.Should().Be(2);
        p1.ManaPool[ManaColor.Red].Should().Be(0);
    }

    [Fact]
    public async Task SiegeGangCommander_InsufficientMana_CannotActivate()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var sgc = GameCard.Create("Siege-Gang Commander");
        p1.Battlefield.Add(sgc);

        var goblinToken = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], IsToken = true };
        p1.Battlefield.Add(goblinToken);

        // No mana in pool
        var target = new GameCard { Name = "Bird", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(target);

        var action = GameAction.ActivateAbility(p1.Id, sgc.Id, targetId: target.Id);
        await engine.ExecuteAction(action);

        // Should not have activated
        target.DamageMarked.Should().Be(0);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == goblinToken.Id);
        state.GameLog.Should().Contain(l => l.Contains("not enough mana"));
    }

    // === Rishadan Port: tap self + pay {1}, tap target land ===

    [Fact]
    public async Task RishadanPort_TapAndPayMana_TapsTargetLand()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var port = GameCard.Create("Rishadan Port");
        p1.Battlefield.Add(port);

        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var targetLand = new GameCard { Name = "Island", CardTypes = CardType.Land };
        p2.Battlefield.Add(targetLand);

        var action = GameAction.ActivateAbility(p1.Id, port.Id, targetId: targetLand.Id);
        await engine.ExecuteAction(action);

        port.IsTapped.Should().BeTrue();
        targetLand.IsTapped.Should().BeTrue();
        p1.ManaPool.Total.Should().Be(0);
    }

    // === Wasteland: tap self + sacrifice self, destroy target land ===

    [Fact]
    public async Task Wasteland_TapAndSacrificeSelf_DestroysTargetLand()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var wasteland = GameCard.Create("Wasteland");
        p1.Battlefield.Add(wasteland);

        var targetLand = new GameCard { Name = "Tropical Island", CardTypes = CardType.Land };
        p2.Battlefield.Add(targetLand);

        var action = GameAction.ActivateAbility(p1.Id, wasteland.Id, targetId: targetLand.Id);
        await engine.ExecuteAction(action);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == wasteland.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == wasteland.Id);
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == targetLand.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Id == targetLand.Id);
    }

    // === Goblin Tinkerer: sacrifice self, destroy target artifact ===

    [Fact]
    public async Task GoblinTinkerer_SacrificeSelf_DestroysArtifact()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var tinkerer = GameCard.Create("Goblin Tinkerer");
        p1.Battlefield.Add(tinkerer);

        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        p2.Battlefield.Add(artifact);

        var action = GameAction.ActivateAbility(p1.Id, tinkerer.Id, targetId: artifact.Id);
        await engine.ExecuteAction(action);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == tinkerer.Id);
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Id == artifact.Id);
    }

    // === Seal of Cleansing: sacrifice self, destroy artifact or enchantment ===

    [Fact]
    public async Task SealOfCleansing_SacrificeSelf_DestroysEnchantment()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var seal = GameCard.Create("Seal of Cleansing");
        p1.Battlefield.Add(seal);

        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        p2.Battlefield.Add(enchantment);

        var action = GameAction.ActivateAbility(p1.Id, seal.Id, targetId: enchantment.Id);
        await engine.ExecuteAction(action);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == seal.Id);
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
    }

    // === Sterling Grove: sacrifice self + pay {1}, search library for enchantment ===

    [Fact]
    public async Task SterlingGrove_SacrificeSelfAndPay_SearchesForEnchantment()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        var grove = GameCard.Create("Sterling Grove");
        p1.Battlefield.Add(grove);

        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var enchantmentInLib = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        p1.Library.Add(enchantmentInLib);

        h1.EnqueueCardChoice(enchantmentInLib.Id);

        var action = GameAction.ActivateAbility(p1.Id, grove.Id);
        await engine.ExecuteAction(action);

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == grove.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == grove.Id);
        p1.Hand.Cards.Should().Contain(c => c.Id == enchantmentInLib.Id);
    }

    // === Card not on battlefield ===

    [Fact]
    public async Task ActivateAbility_CardNotOnBattlefield_DoesNothing()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        var fanatic = GameCard.Create("Mogg Fanatic");
        p1.Hand.Add(fanatic); // In hand, not battlefield

        var action = GameAction.ActivateAbility(p1.Id, fanatic.Id);
        await engine.ExecuteAction(action);

        // Nothing should happen
        p1.Hand.Cards.Should().Contain(c => c.Id == fanatic.Id);
    }

    // === Card with no registered ability ===

    [Fact]
    public async Task ActivateAbility_NoRegisteredAbility_Logs()
    {
        var (engine, state, p1, _, h1, _) = CreateSetup();
        var creature = new GameCard { Name = "UnknownCreature99", CardTypes = CardType.Creature };
        p1.Battlefield.Add(creature);

        var action = GameAction.ActivateAbility(p1.Id, creature.Id);
        await engine.ExecuteAction(action);

        state.GameLog.Should().Contain(l => l.Contains("no activated ability"));
    }

    // === DealDamage to player via activated ability ===

    [Fact]
    public async Task GoblinSharpshooter_DealsDamageToPlayer()
    {
        var (engine, state, p1, p2, h1, _) = CreateSetup();
        var sharpshooter = GameCard.Create("Goblin Sharpshooter");
        p1.Battlefield.Add(sharpshooter);

        var action = GameAction.ActivateAbility(p1.Id, sharpshooter.Id, targetPlayerId: p2.Id);
        await engine.ExecuteAction(action);

        sharpshooter.IsTapped.Should().BeTrue();
        p2.Life.Should().Be(19);
    }
}
