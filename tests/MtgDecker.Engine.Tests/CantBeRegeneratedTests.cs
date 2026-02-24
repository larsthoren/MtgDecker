using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CantBeRegeneratedTests
{
    [Fact]
    public void Perish_ClearsRegenerationShields_BeforeDestroying()
    {
        var riverBoa = GameCard.Create("River Boa", "Creature — Snake");
        riverBoa.TurnEnteredBattlefield = 0;
        riverBoa.RegenerationShields = 2;

        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));
        state.Player2.Battlefield.Add(riverBoa);

        var perishCard = GameCard.Create("Perish", "Sorcery");
        var spell = new StackObject(perishCard, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        new DestroyAllByColorEffect(ManaColor.Green, CardType.Creature).Resolve(state, spell);

        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "River Boa");
        riverBoa.RegenerationShields.Should().Be(0, "Perish says 'can't be regenerated'");
    }

    [Fact]
    public void Crumble_ClearsRegenerationShields_BeforeDestroying()
    {
        var state = new GameState(
            new Player(Guid.NewGuid(), "P1", new TestDecisionHandler()),
            new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));

        var artifact = new GameCard
        {
            Name = "TestArtifact",
            TypeLine = "Artifact",
            CardTypes = CardType.Artifact,
            ManaCost = ManaCost.Parse("{3}"),
            RegenerationShields = 1,
        };
        state.Player2.Battlefield.Add(artifact);

        var crumbleCard = GameCard.Create("Crumble", "Instant");
        var spell = new StackObject(
            crumbleCard,
            state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(artifact.Id, state.Player2.Id, ZoneType.Battlefield) },
            0);

        new CrumbleEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Name == "TestArtifact");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "TestArtifact");
        artifact.RegenerationShields.Should().Be(0, "Crumble says 'can't be regenerated'");
    }
}
