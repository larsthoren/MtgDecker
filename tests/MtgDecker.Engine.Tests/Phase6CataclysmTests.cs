using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase6CataclysmTests
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

    private static GameCard MakeCard(string name, CardType types)
    {
        return new GameCard { Name = name, CardTypes = types };
    }

    private static StackObject MakeSpell(Player caster)
    {
        var spellCard = new GameCard { Name = "Cataclysm", CardTypes = CardType.Sorcery };
        return new StackObject(spellCard, caster.Id, new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
    }

    [Fact]
    public async Task Cataclysm_EachPlayerKeepsOneOfEachType()
    {
        // Arrange: Both players have multiple creatures, lands, and enchantments
        var (state, p1, p2, h1, h2) = Setup();

        var p1Creature1 = MakeCard("Goblin Lackey", CardType.Creature);
        var p1Creature2 = MakeCard("Siege-Gang Commander", CardType.Creature);
        var p1Land1 = MakeCard("Mountain", CardType.Land);
        var p1Land2 = MakeCard("Forest", CardType.Land);
        var p1Enchantment1 = MakeCard("Rancor", CardType.Enchantment);
        var p1Enchantment2 = MakeCard("Seal of Fire", CardType.Enchantment);

        p1.Battlefield.Add(p1Creature1);
        p1.Battlefield.Add(p1Creature2);
        p1.Battlefield.Add(p1Land1);
        p1.Battlefield.Add(p1Land2);
        p1.Battlefield.Add(p1Enchantment1);
        p1.Battlefield.Add(p1Enchantment2);

        var p2Creature1 = MakeCard("Birds of Paradise", CardType.Creature);
        var p2Creature2 = MakeCard("Llanowar Elves", CardType.Creature);
        var p2Land1 = MakeCard("Plains", CardType.Land);
        var p2Land2 = MakeCard("Swamp", CardType.Land);
        var p2Enchantment1 = MakeCard("Ghostly Prison", CardType.Enchantment);

        p2.Battlefield.Add(p2Creature1);
        p2.Battlefield.Add(p2Creature2);
        p2.Battlefield.Add(p2Land1);
        p2.Battlefield.Add(p2Land2);
        p2.Battlefield.Add(p2Enchantment1);

        // P1 chooses: keep Siege-Gang (creature), Forest (land), Seal of Fire (enchantment)
        h1.EnqueueCardChoice(p1Creature2.Id); // creature choice
        h1.EnqueueCardChoice(p1Enchantment2.Id); // enchantment choice
        h1.EnqueueCardChoice(p1Land2.Id); // land choice

        // P2 chooses: keep Birds (creature), Plains (land), Ghostly Prison (enchantment, auto-kept)
        h2.EnqueueCardChoice(p2Creature1.Id); // creature choice
        // enchantment: only 1 so auto-kept
        h2.EnqueueCardChoice(p2Land1.Id); // land choice

        var spell = MakeSpell(p1);

        // Act
        var effect = new CataclysmEffect();
        await effect.ResolveAsync(state, spell, h1);

        // Assert: P1 keeps exactly 1 of each type
        p1.Battlefield.Cards.Should().HaveCount(3);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Creature2.Id);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Land2.Id);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Enchantment2.Id);

        // P2 keeps exactly 1 creature, 1 land, 1 enchantment
        p2.Battlefield.Cards.Should().HaveCount(3);
        p2.Battlefield.Cards.Should().Contain(c => c.Id == p2Creature1.Id);
        p2.Battlefield.Cards.Should().Contain(c => c.Id == p2Land1.Id);
        p2.Battlefield.Cards.Should().Contain(c => c.Id == p2Enchantment1.Id);
    }

    [Fact]
    public async Task Cataclysm_PlayerWithNoneOfType_SkipsChoice()
    {
        // Arrange: P1 has no creatures, only lands and enchantments
        var (state, p1, p2, h1, h2) = Setup();

        var p1Land1 = MakeCard("Mountain", CardType.Land);
        var p1Land2 = MakeCard("Forest", CardType.Land);
        var p1Enchantment1 = MakeCard("Rancor", CardType.Enchantment);

        p1.Battlefield.Add(p1Land1);
        p1.Battlefield.Add(p1Land2);
        p1.Battlefield.Add(p1Enchantment1);

        // P1 choices: no creature choice needed, enchantment auto-kept (only 1), land chosen
        h1.EnqueueCardChoice(p1Land1.Id); // land choice

        // P2 has just 1 creature and 1 land
        var p2Creature = MakeCard("Birds of Paradise", CardType.Creature);
        var p2Land = MakeCard("Plains", CardType.Land);
        p2.Battlefield.Add(p2Creature);
        p2.Battlefield.Add(p2Land);
        // Both auto-kept (only 1 of each)

        var spell = MakeSpell(p1);

        // Act
        var effect = new CataclysmEffect();
        await effect.ResolveAsync(state, spell, h1);

        // Assert: P1 keeps 1 land + 1 enchantment (no creatures to keep)
        p1.Battlefield.Cards.Should().HaveCount(2);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Land1.Id);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Enchantment1.Id);

        // P2 keeps 1 creature + 1 land (auto-kept)
        p2.Battlefield.Cards.Should().HaveCount(2);
        p2.Battlefield.Cards.Should().Contain(c => c.Id == p2Creature.Id);
        p2.Battlefield.Cards.Should().Contain(c => c.Id == p2Land.Id);
    }

    [Fact]
    public async Task Cataclysm_AutoKeepsWhenOnlyOne()
    {
        // Arrange: P1 has exactly 1 of each type -- should auto-keep without ChooseCard
        var (state, p1, p2, h1, h2) = Setup();

        var p1Creature = MakeCard("Goblin Lackey", CardType.Creature);
        var p1Land = MakeCard("Mountain", CardType.Land);
        var p1Enchantment = MakeCard("Rancor", CardType.Enchantment);
        var p1Artifact = MakeCard("Sol Ring", CardType.Artifact);

        p1.Battlefield.Add(p1Creature);
        p1.Battlefield.Add(p1Land);
        p1.Battlefield.Add(p1Enchantment);
        p1.Battlefield.Add(p1Artifact);

        // No choices should be needed for P1 -- all auto-kept
        // We do NOT enqueue any card choices for h1. If ChooseCard is called
        // when it shouldn't be, the default behavior picks first which would
        // still work, but we verify by checking counts.

        // P2 has nothing
        var spell = MakeSpell(p1);

        // Act
        var effect = new CataclysmEffect();
        await effect.ResolveAsync(state, spell, h1);

        // Assert: P1 keeps all 4 (one of each type)
        p1.Battlefield.Cards.Should().HaveCount(4);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Creature.Id);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Land.Id);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Enchantment.Id);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == p1Artifact.Id);

        // P2 has nothing, nothing happens
        p2.Battlefield.Cards.Should().HaveCount(0);
    }

    [Fact]
    public async Task Cataclysm_SacrificesGoToGraveyard()
    {
        // Arrange: P1 has 3 creatures, 2 lands -- sacrifice extras go to graveyard
        var (state, p1, p2, h1, h2) = Setup();

        var creature1 = MakeCard("Goblin Lackey", CardType.Creature);
        var creature2 = MakeCard("Siege-Gang Commander", CardType.Creature);
        var creature3 = MakeCard("Mogg Fanatic", CardType.Creature);
        var land1 = MakeCard("Mountain", CardType.Land);
        var land2 = MakeCard("Forest", CardType.Land);

        p1.Battlefield.Add(creature1);
        p1.Battlefield.Add(creature2);
        p1.Battlefield.Add(creature3);
        p1.Battlefield.Add(land1);
        p1.Battlefield.Add(land2);

        // P1 keeps creature2 and land1
        h1.EnqueueCardChoice(creature2.Id); // creature choice
        h1.EnqueueCardChoice(land1.Id); // land choice

        var spell = MakeSpell(p1);

        // Act
        var effect = new CataclysmEffect();
        await effect.ResolveAsync(state, spell, h1);

        // Assert: kept cards on battlefield
        p1.Battlefield.Cards.Should().HaveCount(2);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == creature2.Id);
        p1.Battlefield.Cards.Should().Contain(c => c.Id == land1.Id);

        // Sacrificed cards in graveyard
        p1.Graveyard.Cards.Should().HaveCount(3);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == creature1.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == creature3.Id);
        p1.Graveyard.Cards.Should().Contain(c => c.Id == land2.Id);
    }
}
