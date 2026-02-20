using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SneakShowIntegrationTests
{
    private static (GameEngine engine, GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) SetupGame()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2, h1, h2);
    }

    [Fact]
    public async Task ShowAndTell_IntoEmrakul_EmrakulOnBattlefield_NoExtraTurn()
    {
        // Show and Tell puts Emrakul into play (not cast), so no extra turn trigger
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.TurnNumber = 2;
        state.CurrentPhase = Phase.MainPhase1;

        // Give mana for Show and Tell ({1}{U}{U})
        p1.ManaPool.Add(ManaColor.Blue, 2);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        // Put Show and Tell in hand
        var showAndTell = GameCard.Create("Show and Tell");
        p1.Hand.Add(showAndTell);

        // Emrakul in hand
        var emrakul = new GameCard
        {
            Name = "Emrakul, the Aeons Torn",
            CardTypes = CardType.Creature,
            BasePower = 15,
            BaseToughness = 15,
        };
        p1.Hand.Add(emrakul);

        // Queue choices: P1 picks Emrakul, P2 declines
        h1.EnqueueCardChoice(emrakul.Id);
        h2.EnqueueCardChoice((Guid?)null);

        // Cast Show and Tell (puts on stack)
        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, showAndTell.Id));

        // Stack should have the spell
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);

        // Resolve the stack
        await engine.ResolveAllTriggersAsync();

        // Emrakul should be on P1's battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul, the Aeons Torn");

        // No extra turn because Emrakul was not cast (put into play via Show and Tell)
        state.ExtraTurns.Should().BeEmpty();
    }

    [Fact]
    public async Task Griselbrand_Draw7_Pays7Life()
    {
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.TurnNumber = 2;
        state.CurrentPhase = Phase.MainPhase1;

        // Put Griselbrand on battlefield (not via summoning — avoid summoning sickness by using early turn)
        var griselbrand = GameCard.Create("Griselbrand");
        griselbrand.TurnEnteredBattlefield = 0; // entered before this turn
        p1.Battlefield.Add(griselbrand);

        // Add 7 cards to library so draw succeeds
        for (int i = 0; i < 7; i++)
            p1.Library.Add(new GameCard { Name = $"Card{i}", CardTypes = CardType.Creature });

        // Activate Griselbrand's ability (PayLife:7, Draw 7)
        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, griselbrand.Id));

        // Ability goes on the stack
        state.StackCount.Should().Be(1);

        // Life is paid immediately as part of cost
        p1.Life.Should().Be(13); // 20 - 7

        // Resolve the stack
        await engine.ResolveAllTriggersAsync();

        // Should have drawn 7 cards
        p1.Hand.Cards.Should().HaveCount(7);
    }

    [Fact]
    public async Task AncientTomb_Tap_Produces2ColorlessDeals2Damage()
    {
        var (engine, state, p1, _, _, _) = SetupGame();
        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;

        var tomb = GameCard.Create("Ancient Tomb", "Land");
        p1.Battlefield.Add(tomb);

        await engine.ExecuteAction(GameAction.TapCard(p1.Id, tomb.Id));

        p1.ManaPool[ManaColor.Colorless].Should().Be(2);
        p1.Life.Should().Be(18); // 20 - 2
    }

    [Fact]
    public async Task LotusPetal_SacrificeForMana()
    {
        var (engine, state, p1, _, h1, _) = SetupGame();
        state.ActivePlayer = p1;
        state.TurnNumber = 2;
        state.CurrentPhase = Phase.MainPhase1;

        var petal = GameCard.Create("Lotus Petal");
        petal.TurnEnteredBattlefield = 0; // not summoning sick (though artifacts don't have SS)
        p1.Battlefield.Add(petal);

        h1.EnqueueManaColor(ManaColor.Blue); // Choose blue mana

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, petal.Id));

        // SacrificeSelf cost: petal is removed from battlefield immediately
        p1.Battlefield.Cards.Should().NotContain(c => c.Name == "Lotus Petal");
        p1.Graveyard.Cards.Should().Contain(c => c.Name == "Lotus Petal");

        // Ability on the stack
        state.StackCount.Should().Be(1);

        // Resolve the stack
        await engine.ResolveAllTriggersAsync();

        // Should have added blue mana
        p1.ManaPool[ManaColor.Blue].Should().Be(1);
    }

    [Fact]
    public async Task ShowAndTell_EmrakulDoesNotTriggerCastTrigger()
    {
        // Verify specifically that the SelfIsCast trigger on Emrakul does not fire
        // when put into play via Show and Tell (it was NOT cast)
        var (engine, state, p1, p2, h1, h2) = SetupGame();
        state.ActivePlayer = p1;
        state.TurnNumber = 2;
        state.CurrentPhase = Phase.MainPhase1;

        // Give mana for Show and Tell
        p1.ManaPool.Add(ManaColor.Blue, 2);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var showAndTell = GameCard.Create("Show and Tell");
        p1.Hand.Add(showAndTell);

        // Use the real Emrakul card from CardDefinitions to get the triggers
        var emrakul = GameCard.Create("Emrakul, the Aeons Torn");
        p1.Hand.Add(emrakul);

        h1.EnqueueCardChoice(emrakul.Id);
        h2.EnqueueCardChoice((Guid?)null);

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, showAndTell.Id));
        await engine.ResolveAllTriggersAsync();

        // Emrakul on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul, the Aeons Torn");

        // No extra turns (the SelfIsCast trigger should not have fired)
        state.ExtraTurns.Should().BeEmpty();
    }

    [Fact]
    public async Task SneakAttack_PutsCreatureOnBattlefield()
    {
        // Test the Sneak Attack activated ability effect directly
        var (_, state, p1, _, h1, _) = SetupGame();
        state.ActivePlayer = p1;

        var emrakul = new GameCard
        {
            Name = "Emrakul",
            CardTypes = CardType.Creature,
            BasePower = 15,
            BaseToughness = 15,
        };
        p1.Hand.Add(emrakul);

        h1.EnqueueCardChoice(emrakul.Id);

        var sneakAttack = new GameCard { Name = "Sneak Attack", CardTypes = CardType.Enchantment };
        var ctx = new EffectContext(state, p1, sneakAttack, h1);

        var effect = new SneakAttackPutEffect();
        await effect.Execute(ctx);

        // Emrakul should be on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul");

        // Should have a delayed sacrifice trigger
        state.DelayedTriggers.Should().ContainSingle(d => d.FireOn == GameEvent.EndStep);

        // Should have haste
        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Haste);
    }

    [Fact]
    public void Pyroclasm_Deals2DamageToAllCreatures()
    {
        var (_, state, p1, p2, _, _) = SetupGame();

        // Setup creatures
        var bearP1 = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p1.Battlefield.Add(bearP1);
        var goblinP2 = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(goblinP2);

        // Resolve Pyroclasm effect
        var pyroclasmCard = new GameCard { Name = "Pyroclasm", CardTypes = CardType.Sorcery };
        var spell = new StackObject(pyroclasmCard, p1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new DamageAllCreaturesEffect(2);
        effect.Resolve(state, spell);

        bearP1.DamageMarked.Should().Be(2);
        goblinP2.DamageMarked.Should().Be(2);
    }

    [Fact]
    public void Pyroblast_CountersBlueSpell()
    {
        var (_, state, p1, p2, _, _) = SetupGame();

        // Put a blue spell on the stack
        var brainstorm = new GameCard
        {
            Name = "Brainstorm",
            CardTypes = CardType.Instant,
            ManaCost = ManaCost.Parse("{U}"),
        };
        var blueSpell = new StackObject(brainstorm, p2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(blueSpell);

        // Pyroblast targeting the blue spell
        var pyroblastCard = new GameCard { Name = "Pyroblast", CardTypes = CardType.Instant };
        var pyroblastSpell = new StackObject(pyroblastCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(brainstorm.Id, p2.Id, ZoneType.Stack) }, 1);

        var effect = new PyroblastEffect();
        effect.Resolve(state, pyroblastSpell);

        // Blue spell should be countered (removed from stack, in graveyard)
        state.Stack.OfType<StackObject>().Should().NotContain(so => so.Card.Name == "Brainstorm");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Brainstorm");
    }

    [Fact]
    public void Pyroblast_FizzlesOnNonBlueSpell()
    {
        var (_, state, p1, p2, _, _) = SetupGame();

        // Put a red spell on the stack
        var bolt = new GameCard
        {
            Name = "Lightning Bolt",
            CardTypes = CardType.Instant,
            ManaCost = ManaCost.Parse("{R}"),
        };
        var redSpell = new StackObject(bolt, p2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);
        state.StackPush(redSpell);

        var pyroblastCard = new GameCard { Name = "Pyroblast", CardTypes = CardType.Instant };
        var pyroblastSpell = new StackObject(pyroblastCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(bolt.Id, p2.Id, ZoneType.Stack) }, 1);

        var effect = new PyroblastEffect();
        effect.Resolve(state, pyroblastSpell);

        // Red spell should still be on the stack (Pyroblast fizzled)
        state.Stack.OfType<StackObject>().Should().Contain(so => so.Card.Name == "Lightning Bolt");
    }

    [Fact]
    public void Pyroblast_DestroysBluePermament()
    {
        var (_, state, p1, p2, _, _) = SetupGame();

        // Blue permanent on opponent's battlefield
        var delver = new GameCard
        {
            Name = "Delver of Secrets",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{U}"),
        };
        p2.Battlefield.Add(delver);

        var pyroblastCard = new GameCard { Name = "Pyroblast", CardTypes = CardType.Instant };
        var pyroblastSpell = new StackObject(pyroblastCard, p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(delver.Id, p2.Id, ZoneType.Battlefield) }, 0);

        var effect = new PyroblastEffect();
        effect.Resolve(state, pyroblastSpell);

        // Delver should be destroyed (in graveyard, not on battlefield)
        p2.Battlefield.Cards.Should().NotContain(c => c.Name == "Delver of Secrets");
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Delver of Secrets");
    }

    [Fact]
    public void BloodMoon_AffectsOpponentLands()
    {
        var (engine, state, p1, p2, _, _) = SetupGame();

        // Nonbasic land on opponent's side
        var volcanicIsland = new GameCard
        {
            Name = "Volcanic Island",
            CardTypes = CardType.Land,
            BaseManaAbility = ManaAbility.Choice(ManaColor.Blue, ManaColor.Red),
            ManaAbility = ManaAbility.Choice(ManaColor.Blue, ManaColor.Red),
        };
        p2.Battlefield.Add(volcanicIsland);

        // Add Blood Moon for P1
        var bloodMoon = new GameCard
        {
            Name = "Blood Moon",
            CardTypes = CardType.Enchantment,
        };
        CardDefinitions.TryGet("Blood Moon", out var def);
        p1.Battlefield.Add(bloodMoon);

        foreach (var ce in def!.ContinuousEffects)
        {
            state.ActiveEffects.Add(ce with { SourceId = bloodMoon.Id });
        }

        engine.RecalculateState();

        // Opponent's nonbasic land should produce Red only
        volcanicIsland.ManaAbility!.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void CityOfTraitors_IsRegisteredWithCorrectAbility()
    {
        CardDefinitions.TryGet("City of Traitors", out var def).Should().BeTrue();
        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.ProduceCount.Should().Be(2);
        def.ManaAbility.FixedColor.Should().Be(ManaColor.Colorless);
    }

    [Fact]
    public void SpellPierce_IsRegisteredWithCounterEffect()
    {
        CardDefinitions.TryGet("Spell Pierce", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.TargetFilter.Should().NotBeNull();
        def.Effect.Should().BeOfType<ConditionalCounterEffect>();
    }

    [Fact]
    public void Intuition_IsRegisteredWithEffect()
    {
        CardDefinitions.TryGet("Intuition", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Instant);
        def.Effect.Should().NotBeNull();
    }
}
