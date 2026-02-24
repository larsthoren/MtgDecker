using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

// ReSharper disable InconsistentNaming

namespace MtgDecker.Engine.Tests;

public class HateCardsTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
            p2.Library.Add(new GameCard { Name = $"Card{i}", TypeLine = "Creature" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    // ─── Card Registration ─────────────────────────────────────────────────

    [Fact]
    public void EngineeredPlague_IsRegistered()
    {
        CardDefinitions.TryGet("Engineered Plague", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Black).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void MeddlingMage_IsRegistered()
    {
        CardDefinitions.TryGet("Meddling Mage", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Creature);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Blue).WhoseValue.Should().Be(1);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
        def.Subtypes.Should().BeEquivalentTo(new[] { "Human", "Wizard" });
    }

    [Fact]
    public void EnsnaringBridge_IsRegistered()
    {
        CardDefinitions.TryGet("Ensnaring Bridge", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Artifact);
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
        def.ManaCost.GenericCost.Should().Be(3);
    }

    [Fact]
    public void TsabosWeb_IsRegistered()
    {
        CardDefinitions.TryGet("Tsabo's Web", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Artifact);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.GenericCost.Should().Be(2);
    }

    // ─── ChooseCreatureType / ChooseCardName on IPlayerDecisionHandler ────

    [Fact]
    public async Task TestDecisionHandler_ChooseCreatureType_DefaultsToGoblin()
    {
        var handler = new TestDecisionHandler();
        var result = await handler.ChooseCreatureType("Choose a type:");
        result.Should().Be("Goblin");
    }

    [Fact]
    public async Task TestDecisionHandler_ChooseCreatureType_ReturnsEnqueued()
    {
        var handler = new TestDecisionHandler();
        handler.EnqueueCreatureType("Elf");
        var result = await handler.ChooseCreatureType("Choose a type:");
        result.Should().Be("Elf");
    }

    [Fact]
    public async Task TestDecisionHandler_ChooseCardName_DefaultsToLightningBolt()
    {
        var handler = new TestDecisionHandler();
        var result = await handler.ChooseCardName("Choose a name:");
        result.Should().Be("Lightning Bolt");
    }

    [Fact]
    public async Task TestDecisionHandler_ChooseCardName_ReturnsEnqueued()
    {
        var handler = new TestDecisionHandler();
        handler.EnqueueCardName("Swords to Plowshares");
        var result = await handler.ChooseCardName("Choose a name:");
        result.Should().Be("Swords to Plowshares");
    }

    // ─── GameCard.ChosenType / ChosenName ────────────────────────────────

    [Fact]
    public void GameCard_ChosenType_DefaultsToNull()
    {
        var card = new GameCard { Name = "Test" };
        card.ChosenType.Should().BeNull();
    }

    [Fact]
    public void GameCard_ChosenName_DefaultsToNull()
    {
        var card = new GameCard { Name = "Test" };
        card.ChosenName.Should().BeNull();
    }

    // ─── ChooseCreatureTypeEffect ────────────────────────────────────────

    [Fact]
    public async Task ChooseCreatureTypeEffect_StoresChosenType()
    {
        var handler = new TestDecisionHandler();
        handler.EnqueueCreatureType("Wizard");

        var player = new Player(Guid.NewGuid(), "P1", handler);
        var state = new GameState(player, new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));
        var source = new GameCard { Name = "Engineered Plague" };
        player.Battlefield.Add(source);

        var context = new EffectContext(state, player, source, handler);
        var effect = new ChooseCreatureTypeEffect();

        await effect.Execute(context);

        source.ChosenType.Should().Be("Wizard");
    }

    // ─── ChooseCardNameEffect ────────────────────────────────────────────

    [Fact]
    public async Task ChooseCardNameEffect_StoresChosenName()
    {
        var handler = new TestDecisionHandler();
        handler.EnqueueCardName("Counterspell");

        var player = new Player(Guid.NewGuid(), "P1", handler);
        var state = new GameState(player, new Player(Guid.NewGuid(), "P2", new TestDecisionHandler()));
        var source = new GameCard { Name = "Meddling Mage" };
        player.Battlefield.Add(source);

        var context = new EffectContext(state, player, source, handler);
        var effect = new ChooseCardNameEffect();

        await effect.Execute(context);

        source.ChosenName.Should().Be("Counterspell");
    }

    // ─── Engineered Plague: ETB + Continuous Effect ──────────────────────

    [Fact]
    public async Task EngineeredPlague_ETB_SetsChosenType_And_GivesMinusOneMinusOne()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // P1 chooses "Goblin"
        p1Handler.EnqueueCreatureType("Goblin");

        // Put a Goblin on the battlefield with 2/2
        var goblin = new GameCard
        {
            Name = "Goblin Piker", TypeLine = "Creature — Goblin",
            CardTypes = CardType.Creature, Subtypes = ["Goblin"],
            BasePower = 2, BaseToughness = 2, TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(goblin);

        // Put a non-Goblin creature on the battlefield
        var elf = new GameCard
        {
            Name = "Llanowar Elves", TypeLine = "Creature — Elf Druid",
            CardTypes = CardType.Creature, Subtypes = ["Elf", "Druid"],
            BasePower = 1, BaseToughness = 1, TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(elf);

        // Cast Engineered Plague
        state.CurrentPhase = Phase.MainPhase1;
        var plague = GameCard.Create("Engineered Plague");
        state.Player1.Hand.Add(plague);
        state.Player1.ManaPool.Add(ManaColor.Black);
        state.Player1.ManaPool.Add(ManaColor.Colorless);
        state.Player1.ManaPool.Add(ManaColor.Colorless);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, plague.Id));
        await engine.ResolveAllTriggersAsync();

        // Engineered Plague should be on the battlefield with ChosenType set
        plague.ChosenType.Should().Be("Goblin");

        // The Goblin should get -1/-1
        engine.RecalculateState();
        goblin.Power.Should().Be(1, "Goblin should get -1 power from Engineered Plague");
        goblin.Toughness.Should().Be(1, "Goblin should get -1 toughness from Engineered Plague");

        // The Elf should not be affected
        elf.Power.Should().Be(1, "Elf should not be affected by Engineered Plague choosing Goblin");
        elf.Toughness.Should().Be(1, "Elf should not be affected by Engineered Plague choosing Goblin");
    }

    [Fact]
    public async Task EngineeredPlague_AffectsOpponentsCreaturesToo()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        p1Handler.EnqueueCreatureType("Goblin");

        // Opponent's Goblin
        var oppGoblin = new GameCard
        {
            Name = "Goblin Token", TypeLine = "Creature — Goblin",
            CardTypes = CardType.Creature, Subtypes = ["Goblin"],
            BasePower = 1, BaseToughness = 1, TurnEnteredBattlefield = 0
        };
        state.Player2.Battlefield.Add(oppGoblin);

        state.CurrentPhase = Phase.MainPhase1;
        var plague = GameCard.Create("Engineered Plague");
        state.Player1.Hand.Add(plague);
        state.Player1.ManaPool.Add(ManaColor.Black);
        state.Player1.ManaPool.Add(ManaColor.Colorless);
        state.Player1.ManaPool.Add(ManaColor.Colorless);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, plague.Id));
        await engine.ResolveAllTriggersAsync();

        engine.RecalculateState();
        oppGoblin.Power.Should().Be(0, "opponent's Goblin should get -1 power");
        oppGoblin.Toughness.Should().Be(0, "opponent's Goblin should get -1 toughness");
    }

    [Fact]
    public async Task EngineeredPlague_MultipleChoices_StackEffects()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // Cast first Engineered Plague naming "Goblin"
        p1Handler.EnqueueCreatureType("Goblin");
        state.CurrentPhase = Phase.MainPhase1;
        var plague1 = GameCard.Create("Engineered Plague");
        state.Player1.Hand.Add(plague1);
        state.Player1.ManaPool.Add(ManaColor.Black);
        state.Player1.ManaPool.Add(ManaColor.Colorless);
        state.Player1.ManaPool.Add(ManaColor.Colorless);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, plague1.Id));
        await engine.ResolveAllTriggersAsync();

        // Cast second Engineered Plague naming "Goblin" again
        p1Handler.EnqueueCreatureType("Goblin");
        var plague2 = GameCard.Create("Engineered Plague");
        state.Player1.Hand.Add(plague2);
        state.Player1.ManaPool.Add(ManaColor.Black);
        state.Player1.ManaPool.Add(ManaColor.Colorless);
        state.Player1.ManaPool.Add(ManaColor.Colorless);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, plague2.Id));
        await engine.ResolveAllTriggersAsync();

        // Put a 3/3 goblin on battlefield
        var goblin = new GameCard
        {
            Name = "Goblin Chieftain", TypeLine = "Creature — Goblin",
            CardTypes = CardType.Creature, Subtypes = ["Goblin"],
            BasePower = 3, BaseToughness = 3, TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(goblin);

        engine.RecalculateState();
        goblin.Power.Should().Be(1, "Two Engineered Plagues naming Goblin should give -2/-2");
        goblin.Toughness.Should().Be(1, "Two Engineered Plagues naming Goblin should give -2/-2");
    }

    // ─── Meddling Mage: ETB + Spell Prevention ──────────────────────────

    [Fact]
    public async Task MeddlingMage_ETB_SetsChosenName()
    {
        var (engine, state, _, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        p2Handler.EnqueueCardName("Lightning Bolt");

        state.CurrentPhase = Phase.MainPhase1;
        // P2 casts Meddling Mage
        state.ActivePlayer = state.Player2;
        var mage = GameCard.Create("Meddling Mage");
        state.Player2.Hand.Add(mage);
        state.Player2.ManaPool.Add(ManaColor.White);
        state.Player2.ManaPool.Add(ManaColor.Blue);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player2.Id, mage.Id));
        await engine.ResolveAllTriggersAsync();

        mage.ChosenName.Should().Be("Lightning Bolt");
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Id == mage.Id);
    }

    [Fact]
    public async Task MeddlingMage_PreventsOpponentFromCastingNamedSpell()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // P2 has Meddling Mage naming "Lightning Bolt"
        var mage = GameCard.Create("Meddling Mage");
        mage.ChosenName = "Lightning Bolt";
        state.Player2.Battlefield.Add(mage);

        // P1 tries to cast Lightning Bolt
        state.CurrentPhase = Phase.MainPhase1;
        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);
        state.Player1.ManaPool.Add(ManaColor.Red);

        var handCountBefore = state.Player1.Hand.Cards.Count;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));

        // Spell should not have been cast
        state.Player1.Hand.Cards.Count.Should().Be(handCountBefore, "Lightning Bolt should still be in hand");
        state.StackCount.Should().Be(0, "Nothing should be on the stack");
    }

    [Fact]
    public async Task MeddlingMage_DoesNotPreventUnnamedSpell()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // P2 has Meddling Mage naming "Lightning Bolt"
        var mage = GameCard.Create("Meddling Mage");
        mage.ChosenName = "Lightning Bolt";
        state.Player2.Battlefield.Add(mage);

        // P1 casts Swords to Plowshares (a different spell)
        state.CurrentPhase = Phase.MainPhase1;
        var stp = GameCard.Create("Swords to Plowshares");
        state.Player1.Hand.Add(stp);
        state.Player1.ManaPool.Add(ManaColor.White);

        // STP needs a target — provide P2's creature
        var targetCreature = new GameCard
        {
            Name = "Bear", TypeLine = "Creature — Bear",
            CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2
        };
        state.Player2.Battlefield.Add(targetCreature);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, stp.Id));

        // Swords should have been cast (on the stack)
        state.StackCount.Should().BeGreaterThan(0, "Swords to Plowshares is not named by Meddling Mage");
    }

    [Fact]
    public async Task MeddlingMage_NamedSpellIsBlockedCaseInsensitive()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // P2 has Meddling Mage naming "lightning bolt" (lowercase)
        var mage = GameCard.Create("Meddling Mage");
        mage.ChosenName = "lightning bolt";
        state.Player2.Battlefield.Add(mage);

        state.CurrentPhase = Phase.MainPhase1;
        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);
        state.Player1.ManaPool.Add(ManaColor.Red);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));

        // Still blocked despite different case
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == bolt.Id, "case-insensitive comparison should block");
        state.StackCount.Should().Be(0);
    }

    // ─── Ensnaring Bridge: Attack Restriction ────────────────────────────

    [Fact]
    public async Task EnsnaringBridge_PreventsCreaturesWithPowerGreaterThanHandSize()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        // Put Ensnaring Bridge on battlefield for P2
        var bridge = GameCard.Create("Ensnaring Bridge");
        state.Player2.Battlefield.Add(bridge);

        // P1 has a 4/4 creature ready to attack
        var bigCreature = new GameCard
        {
            Name = "Big Creature", TypeLine = "Creature",
            CardTypes = CardType.Creature, BasePower = 4, BaseToughness = 4,
            TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(bigCreature);

        // P2 (bridge controller) has 3 cards in hand — only creatures with power <= 3 can attack
        state.Player2.Hand.Clear();
        state.Player2.Hand.Add(new GameCard { Name = "H1" });
        state.Player2.Hand.Add(new GameCard { Name = "H2" });
        state.Player2.Hand.Add(new GameCard { Name = "H3" });

        // P1 tries to attack
        p1Handler.EnqueueAttackers(new List<Guid> { bigCreature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        // Big creature has power 4 > 3 (bridge controller's hand size), so it can't attack
        state.Player2.Life.Should().Be(20, "creature with power > bridge controller's hand size can't attack through Ensnaring Bridge");
    }

    [Fact]
    public async Task EnsnaringBridge_AllowsCreaturesWithPowerEqualToHandSize()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var bridge = GameCard.Create("Ensnaring Bridge");
        state.Player2.Battlefield.Add(bridge);

        // 2/2 creature
        var creature = new GameCard
        {
            Name = "Bear", TypeLine = "Creature — Bear",
            CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2,
            TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(creature);

        // P2 (bridge controller) has exactly 2 cards in hand
        state.Player2.Hand.Clear();
        state.Player2.Hand.Add(new GameCard { Name = "H1" });
        state.Player2.Hand.Add(new GameCard { Name = "H2" });

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        // Power 2 <= 2 (bridge controller's hand size) — can attack
        state.Player2.Life.Should().Be(18, "creature with power equal to bridge controller's hand size can attack");
    }

    [Fact]
    public async Task EnsnaringBridge_EmptyHand_PreventsAllCreatures()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var bridge = GameCard.Create("Ensnaring Bridge");
        state.Player2.Battlefield.Add(bridge);

        // 1/1 creature
        var creature = new GameCard
        {
            Name = "Token", TypeLine = "Creature",
            CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1,
            TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(creature);

        // P2 (bridge controller) has empty hand
        state.Player2.Hand.Clear();

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        // Power 1 > 0 (bridge controller's hand size) — can't attack
        state.Player2.Life.Should().Be(20, "with bridge controller's empty hand, no creature with power > 0 can attack");
    }

    [Fact]
    public async Task EnsnaringBridge_AllowsZeroPowerCreatureWithEmptyHand()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var bridge = GameCard.Create("Ensnaring Bridge");
        state.Player2.Battlefield.Add(bridge);

        // 0/1 creature
        var creature = new GameCard
        {
            Name = "Wall", TypeLine = "Creature",
            CardTypes = CardType.Creature, BasePower = 0, BaseToughness = 1,
            TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(creature);

        // P2 (bridge controller) has empty hand
        state.Player2.Hand.Clear();

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        // Power 0 <= 0 (bridge controller's hand size) — can attack
        state.Player2.Life.Should().Be(20, "0 power creature deals no damage but can attack");
    }

    [Fact]
    public async Task EnsnaringBridge_FromEitherPlayer_RestrictsAttacking()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        // P1 controls the Bridge — checks bridge controller's (P1's) hand size
        var bridge = GameCard.Create("Ensnaring Bridge");
        state.Player1.Battlefield.Add(bridge);

        var creature = new GameCard
        {
            Name = "Big Beast", TypeLine = "Creature",
            CardTypes = CardType.Creature, BasePower = 5, BaseToughness = 5,
            TurnEnteredBattlefield = 0
        };
        state.Player1.Battlefield.Add(creature);

        // P1 has 2 cards in hand — creature power 5 > 2
        state.Player1.Hand.Clear();
        state.Player1.Hand.Add(new GameCard { Name = "H1" });
        state.Player1.Hand.Add(new GameCard { Name = "H2" });

        p1Handler.EnqueueAttackers(new List<Guid> { creature.Id });

        await engine.RunCombatAsync(CancellationToken.None);

        state.Player2.Life.Should().Be(20, "own Ensnaring Bridge also restricts own attacks");
    }

    // ─── Tsabo's Web: ETB Draw ──────────────────────────────────────────

    [Fact]
    public async Task TsabosWeb_ETB_DrawsCard()
    {
        var (engine, state, p1Handler, _) = CreateSetup();
        await engine.StartGameAsync();

        state.CurrentPhase = Phase.MainPhase1;
        var web = GameCard.Create("Tsabo's Web");
        state.Player1.Hand.Add(web);
        state.Player1.ManaPool.Add(ManaColor.Colorless);
        state.Player1.ManaPool.Add(ManaColor.Colorless);

        var handBefore = state.Player1.Hand.Cards.Count;

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, web.Id));
        await engine.ResolveAllTriggersAsync();

        // Tsabo's Web ETB triggers a card draw
        // Hand should have gained 1 card (drew 1) minus 1 (cast web) = net 0 change
        // Actually: Web leaves hand (-1), enters battlefield, ETB draws a card (+1), net = 0
        state.Player1.Hand.Cards.Count.Should().Be(handBefore,
            "Tsabo's Web ETB draws a card, replacing itself in hand");
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == web.Id);
    }

    [Fact]
    public async Task TsabosWeb_HasETBDrawTrigger()
    {
        CardDefinitions.TryGet("Tsabo's Web", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Effect.Should().BeOfType<DrawCardEffect>();
    }

    // ─── DynamicContinuousEffectsFactory ────────────────────────────────

    [Fact]
    public void EngineeredPlague_HasDynamicContinuousEffectsFactory()
    {
        CardDefinitions.TryGet("Engineered Plague", out var def).Should().BeTrue();
        def!.DynamicContinuousEffectsFactory.Should().NotBeNull();
    }

    [Fact]
    public void EngineeredPlague_Factory_ReturnsEmptyWhenNoChosenType()
    {
        CardDefinitions.TryGet("Engineered Plague", out var def);
        var card = new GameCard { Name = "Engineered Plague" };
        var effects = def!.DynamicContinuousEffectsFactory!(card);
        effects.Should().BeEmpty();
    }

    [Fact]
    public void EngineeredPlague_Factory_ReturnsEffectWhenChosenTypeSet()
    {
        CardDefinitions.TryGet("Engineered Plague", out var def);
        var card = new GameCard { Name = "Engineered Plague", ChosenType = "Goblin" };
        var effects = def!.DynamicContinuousEffectsFactory!(card);
        effects.Should().HaveCount(1);
        effects[0].Type.Should().Be(ContinuousEffectType.ModifyPowerToughness);
        effects[0].PowerMod.Should().Be(-1);
        effects[0].ToughnessMod.Should().Be(-1);
    }
}
