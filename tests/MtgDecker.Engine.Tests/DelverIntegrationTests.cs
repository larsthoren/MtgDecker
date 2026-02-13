using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DelverIntegrationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Delver", h1);
        var p2 = new Player(Guid.NewGuid(), "Opponent", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    /// <summary>
    /// Clear all zones for precise state control after StartGameAsync draws 7 cards.
    /// </summary>
    private static void ClearAllZones(Player player)
    {
        player.Hand.Clear();
        player.Library.Clear();
        player.Graveyard.Clear();
        player.Battlefield.Clear();
    }

    [Fact]
    public async Task Brainstorm_FullFlow_Draw3PutBack2()
    {
        // Setup: Player with Brainstorm in hand, Island on battlefield, 5 known cards in library
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Clear zones for precise state control
        ClearAllZones(state.Player1);

        // Set known cards in library (bottom to top: E, D, C, B, A)
        var libCard1 = new GameCard { Name = "LibA" };
        var libCard2 = new GameCard { Name = "LibB" };
        var libCard3 = new GameCard { Name = "LibC" };
        var libCard4 = new GameCard { Name = "LibD" };
        var libCard5 = new GameCard { Name = "LibE" };
        state.Player1.Library.Add(libCard5);
        state.Player1.Library.Add(libCard4);
        state.Player1.Library.Add(libCard3);
        state.Player1.Library.Add(libCard2);
        state.Player1.Library.Add(libCard1);

        var island = GameCard.Create("Island");
        state.Player1.Battlefield.Add(island);

        var brainstorm = GameCard.Create("Brainstorm");
        state.Player1.Hand.Add(brainstorm);

        // Add two other cards to hand (so we have cards to put back)
        var handCard1 = new GameCard { Name = "HandX" };
        var handCard2 = new GameCard { Name = "HandY" };
        state.Player1.Hand.Add(handCard1);
        state.Player1.Hand.Add(handCard2);

        // Brainstorm draws 3 (LibA, LibB, LibC from top), then puts back 2
        // Choose to put back HandX and HandY
        h1.EnqueueCardChoice(handCard1.Id); // first card to put back
        h1.EnqueueCardChoice(handCard2.Id); // second card to put back

        // Actions: Tap Island, Cast Brainstorm
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, island.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, brainstorm.Id));

        await engine.RunPriorityAsync();

        // After resolution:
        // Original hand: brainstorm, HandX, HandY (3 cards)
        // Cast brainstorm → 2 cards (HandX, HandY)
        // Draw 3 → 5 cards (HandX, HandY, LibA, LibB, LibC)
        // Put back 2 (HandX, HandY) → 3 cards (LibA, LibB, LibC)
        state.Player1.Hand.Count.Should().Be(3);
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "LibA");
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "LibB");
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "LibC");

        // Library top should be HandY (put back second = on top), then HandX
        var topCards = state.Player1.Library.PeekTop(2);
        topCards[0].Name.Should().Be("HandY"); // most recently put back = on top
        topCards[1].Name.Should().Be("HandX");

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Brainstorm");
    }

    [Fact]
    public async Task Counterspell_CountersLightningBolt()
    {
        // P1 casts Lightning Bolt, P2 counters it with Counterspell
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // P1 setup: Mountain + Lightning Bolt
        var mountain = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain);
        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);

        // P2 setup: 2 Islands + Counterspell
        var island1 = GameCard.Create("Island");
        var island2 = GameCard.Create("Island");
        state.Player2.Battlefield.Add(island1);
        state.Player2.Battlefield.Add(island2);
        var counter = GameCard.Create("Counterspell");
        state.Player2.Hand.Add(counter);

        // P1 targets P2 with Lightning Bolt
        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));

        // P2 targets the Bolt on the stack with Counterspell
        h2.EnqueueTarget(new TargetInfo(bolt.Id, state.Player1.Id, ZoneType.Stack));

        // P1 actions: tap Mountain, cast Bolt
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));

        // P2 actions: tap Islands, cast Counterspell
        // Priority flow: P1 acts (tap, cast) → P1 passes → P2 taps (priority bounces) → P2 casts → both pass
        h2.EnqueueAction(GameAction.TapCard(state.Player2.Id, island1.Id));
        h2.EnqueueAction(GameAction.TapCard(state.Player2.Id, island2.Id));
        h2.EnqueueAction(GameAction.CastSpell(state.Player2.Id, counter.Id));

        await engine.RunPriorityAsync();

        // Counterspell resolves first (LIFO), countering Bolt
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
        state.Player2.Life.Should().Be(20);
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task Ponder_FullFlow_ShuffleAndDraw()
    {
        // Cast Ponder, choose to shuffle, draw 1
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        ClearAllZones(state.Player1);

        var lib1 = new GameCard { Name = "TopCard" };
        var lib2 = new GameCard { Name = "MiddleCard" };
        var lib3 = new GameCard { Name = "BottomCard" };
        var lib4 = new GameCard { Name = "DeepCard" };
        state.Player1.Library.Add(lib4);
        state.Player1.Library.Add(lib3);
        state.Player1.Library.Add(lib2);
        state.Player1.Library.Add(lib1); // lib1 is on top

        var island = GameCard.Create("Island");
        state.Player1.Battlefield.Add(island);

        var ponder = GameCard.Create("Ponder");
        state.Player1.Hand.Add(ponder);

        // Ponder flow:
        // 1. RevealCards (auto-acknowledged by TestDecisionHandler)
        // 2. ChooseCard with empty list + optional=true → null means "shuffle"
        h1.EnqueueCardChoice(null); // choose to shuffle

        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, island.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, ponder.Id));

        await engine.RunPriorityAsync();

        // After Ponder: cast ponder (hand -1), draw 1 (hand +1) = net 0
        state.Player1.Hand.Count.Should().Be(1);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Ponder");
        state.Player1.Library.Count.Should().Be(3);
    }

    [Fact]
    public async Task Ponder_KeepOrder_DrawTopCard()
    {
        // Cast Ponder, choose to keep order, draw the top card
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        ClearAllZones(state.Player1);

        var lib1 = new GameCard { Name = "TopCard" };
        var lib2 = new GameCard { Name = "SecondCard" };
        var lib3 = new GameCard { Name = "ThirdCard" };
        var lib4 = new GameCard { Name = "FourthCard" };
        state.Player1.Library.Add(lib4);
        state.Player1.Library.Add(lib3);
        state.Player1.Library.Add(lib2);
        state.Player1.Library.Add(lib1);

        var island = GameCard.Create("Island");
        state.Player1.Battlefield.Add(island);

        var ponder = GameCard.Create("Ponder");
        state.Player1.Hand.Add(ponder);

        // Choose to keep order: non-null = keep
        h1.EnqueueCardChoice(Guid.NewGuid());

        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, island.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, ponder.Id));

        await engine.RunPriorityAsync();

        // Drew TopCard (kept order)
        state.Player1.Hand.Count.Should().Be(1);
        state.Player1.Hand.Cards[0].Name.Should().Be("TopCard");
        state.Player1.Library.Count.Should().Be(3);
    }

    [Fact]
    public async Task Preordain_KeepOneBottomOne_DrawCard()
    {
        // Cast Preordain: scry 2 (keep one, bottom one), then draw 1
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        ClearAllZones(state.Player1);

        var lib1 = new GameCard { Name = "KeepMe" };
        var lib2 = new GameCard { Name = "BottomMe" };
        var lib3 = new GameCard { Name = "ThirdCard" };
        state.Player1.Library.Add(lib3);
        state.Player1.Library.Add(lib2);
        state.Player1.Library.Add(lib1); // lib1 on top

        var island = GameCard.Create("Island");
        state.Player1.Battlefield.Add(island);

        var preordain = GameCard.Create("Preordain");
        state.Player1.Hand.Add(preordain);

        // Scry: non-null = keep on top, null = send to bottom
        h1.EnqueueCardChoice(lib1.Id);  // Keep "KeepMe" on top
        h1.EnqueueCardChoice(null);      // Send "BottomMe" to bottom

        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, island.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, preordain.Id));

        await engine.RunPriorityAsync();

        // After resolution: drew "KeepMe" (was kept on top)
        state.Player1.Hand.Count.Should().Be(1);
        state.Player1.Hand.Cards[0].Name.Should().Be("KeepMe");
        state.Player1.Library.Count.Should().Be(2);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Preordain");
    }

    [Fact]
    public async Task CounterWar_CounterTargetsCounter()
    {
        // P1 casts a creature, P2 counters it, P1 counters the counter
        // LIFO: P1's counter resolves first, removing P2's counter
        // Then the creature resolves normally
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // P1 setup: 2 Islands + Volcanic Island + Delver of Secrets + Counterspell
        var island1 = GameCard.Create("Island");
        var island2 = GameCard.Create("Island");
        var volc = GameCard.Create("Volcanic Island");
        state.Player1.Battlefield.Add(island1);
        state.Player1.Battlefield.Add(island2);
        state.Player1.Battlefield.Add(volc);

        var delver = GameCard.Create("Delver of Secrets");
        state.Player1.Hand.Add(delver);
        var p1Counter = GameCard.Create("Counterspell");
        state.Player1.Hand.Add(p1Counter);

        // P2 setup: 2 Islands + Counterspell
        var p2Island1 = GameCard.Create("Island");
        var p2Island2 = GameCard.Create("Island");
        state.Player2.Battlefield.Add(p2Island1);
        state.Player2.Battlefield.Add(p2Island2);
        var p2Counter = GameCard.Create("Counterspell");
        state.Player2.Hand.Add(p2Counter);

        // The counter-war needs to use direct ExecuteAction calls to control
        // the exact sequence, since RunPriorityAsync bounces priority between
        // the active player (P1) and non-active player (P2).
        //
        // We'll build the stack manually, then let RunPriorityAsync resolve it.

        // Step 1: P1 taps Island, casts Delver
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, island1.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, delver.Id));
        state.Stack.Should().HaveCount(1);

        // Step 2: P2 taps Islands, casts Counterspell targeting Delver
        h2.EnqueueTarget(new TargetInfo(delver.Id, state.Player1.Id, ZoneType.Stack));
        await engine.ExecuteAction(GameAction.TapCard(state.Player2.Id, p2Island1.Id));
        await engine.ExecuteAction(GameAction.TapCard(state.Player2.Id, p2Island2.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player2.Id, p2Counter.Id));
        state.Stack.Should().HaveCount(2);

        // Step 3: P1 taps remaining lands, casts Counterspell targeting P2's Counterspell
        h1.EnqueueManaColor(ManaColor.Blue); // Choose blue from Volcanic Island
        h1.EnqueueTarget(new TargetInfo(p2Counter.Id, state.Player2.Id, ZoneType.Stack));
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, island2.Id));
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, volc.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, p1Counter.Id));
        state.Stack.Should().HaveCount(3);

        // Now resolve the stack via RunPriorityAsync (both auto-pass)
        await engine.RunPriorityAsync();

        // Resolution order (LIFO):
        // 1. P1's Counterspell resolves → counters P2's Counterspell
        // 2. Delver resolves → enters battlefield (creature, no effect)
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Delver of Secrets");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
        state.Stack.Should().BeEmpty();
    }

    [Fact]
    public async Task Counterspell_Fizzles_WhenTargetAlreadyResolved()
    {
        // If the targeted spell leaves the stack before Counterspell resolves, it fizzles
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // P1 setup: Island + Brainstorm
        var island = GameCard.Create("Island");
        state.Player1.Battlefield.Add(island);
        var brainstorm = GameCard.Create("Brainstorm");
        state.Player1.Hand.Add(brainstorm);

        // P2 setup: 2 Islands + Counterspell
        var p2Island1 = GameCard.Create("Island");
        var p2Island2 = GameCard.Create("Island");
        state.Player2.Battlefield.Add(p2Island1);
        state.Player2.Battlefield.Add(p2Island2);
        var counter = GameCard.Create("Counterspell");
        state.Player2.Hand.Add(counter);

        // Build stack via direct ExecuteAction calls
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, island.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, brainstorm.Id));
        state.Stack.Should().HaveCount(1);

        h2.EnqueueTarget(new TargetInfo(brainstorm.Id, state.Player1.Id, ZoneType.Stack));
        await engine.ExecuteAction(GameAction.TapCard(state.Player2.Id, p2Island1.Id));
        await engine.ExecuteAction(GameAction.TapCard(state.Player2.Id, p2Island2.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player2.Id, counter.Id));
        state.Stack.Should().HaveCount(2);

        // Manually remove Brainstorm from the stack (simulating it already resolved)
        var brainstormStack = state.Stack.First(s => s.Card.Name == "Brainstorm");
        state.Stack.Remove(brainstormStack);
        state.Player1.Graveyard.Add(brainstormStack.Card);

        // Resolve remaining stack via RunPriorityAsync (both auto-pass)
        await engine.RunPriorityAsync();

        // Counterspell should fizzle (target no longer on stack)
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");
        state.GameLog.Should().Contain(l => l.Contains("fizzle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Daze_CountersSpell()
    {
        // Daze costs {1}{U} and counters a spell
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // P1 setup: Mountain + Lightning Bolt
        var mountain = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mountain);
        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);

        // P2 setup: 2 Islands + Daze (costs {1}{U})
        var p2Island1 = GameCard.Create("Island");
        var p2Island2 = GameCard.Create("Island");
        state.Player2.Battlefield.Add(p2Island1);
        state.Player2.Battlefield.Add(p2Island2);
        var daze = GameCard.Create("Daze");
        state.Player2.Hand.Add(daze);

        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));
        h2.EnqueueTarget(new TargetInfo(bolt.Id, state.Player1.Id, ZoneType.Stack));

        // P1 taps Mountain, casts Bolt
        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, mountain.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));

        // P2 taps Islands (for {1}{U}), casts Daze
        h2.EnqueueAction(GameAction.TapCard(state.Player2.Id, p2Island1.Id));
        h2.EnqueueAction(GameAction.TapCard(state.Player2.Id, p2Island2.Id));
        h2.EnqueueAction(GameAction.CastSpell(state.Player2.Id, daze.Id));

        await engine.RunPriorityAsync();

        // Daze countered the bolt
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "Daze");
        state.Player2.Life.Should().Be(20);
    }

    [Fact]
    public async Task Brainstorm_WithEmptyLibrary_DrawsLessThan3()
    {
        // If library has fewer than 3 cards, Brainstorm draws what it can
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        ClearAllZones(state.Player1);

        var onlyCard = new GameCard { Name = "OnlyCard" };
        state.Player1.Library.Add(onlyCard);

        var island = GameCard.Create("Island");
        state.Player1.Battlefield.Add(island);

        var brainstorm = GameCard.Create("Brainstorm");
        state.Player1.Hand.Add(brainstorm);

        // Library has 1 card. Brainstorm draws it.
        // Hand after draw: OnlyCard (1 card)
        // Put back min(2, 1) = 1 card
        h1.EnqueueCardChoice(onlyCard.Id); // put OnlyCard back on top

        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, island.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, brainstorm.Id));

        await engine.RunPriorityAsync();

        state.Player1.Hand.Count.Should().Be(0);
        state.Player1.Library.Count.Should().Be(1);
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Brainstorm");
    }

    [Fact]
    public async Task Preordain_BothToBottom_DrawDeepCard()
    {
        // Cast Preordain, scry both cards to bottom, draw the next card
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        ClearAllZones(state.Player1);

        var top = new GameCard { Name = "Scry1" };
        var second = new GameCard { Name = "Scry2" };
        var third = new GameCard { Name = "WantThis" };
        state.Player1.Library.Add(third);
        state.Player1.Library.Add(second);
        state.Player1.Library.Add(top);

        var island = GameCard.Create("Island");
        state.Player1.Battlefield.Add(island);

        var preordain = GameCard.Create("Preordain");
        state.Player1.Hand.Add(preordain);

        // Scry both to bottom (null = send to bottom)
        h1.EnqueueCardChoice(null); // Scry1 -> bottom
        h1.EnqueueCardChoice(null); // Scry2 -> bottom

        h1.EnqueueAction(GameAction.TapCard(state.Player1.Id, island.Id));
        h1.EnqueueAction(GameAction.CastSpell(state.Player1.Id, preordain.Id));

        await engine.RunPriorityAsync();

        // Drew WantThis (was third from top, now on top after scrying both away)
        state.Player1.Hand.Count.Should().Be(1);
        state.Player1.Hand.Cards[0].Name.Should().Be("WantThis");
        state.Player1.Library.Count.Should().Be(2);
    }

    [Fact]
    public async Task FullSequence_CastCreature_ThenCountered_ThenBoltResolves()
    {
        // Complex multi-spell scenario using direct ExecuteAction for stack building:
        // P1 casts Goblin Guide, P2 counters, P1 casts Bolt targeting P2
        // Stack (top to bottom): Bolt, Counter, Goblin
        // Resolution: Bolt deals 3 to P2, Counter counters Goblin, Goblin never resolves
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // P1: 2 Mountains + Goblin Guide + Lightning Bolt
        var mtn1 = GameCard.Create("Mountain");
        var mtn2 = GameCard.Create("Mountain");
        state.Player1.Battlefield.Add(mtn1);
        state.Player1.Battlefield.Add(mtn2);
        var goblin = GameCard.Create("Goblin Guide");
        state.Player1.Hand.Add(goblin);
        var bolt = GameCard.Create("Lightning Bolt");
        state.Player1.Hand.Add(bolt);

        // P2: 2 Islands + Counterspell
        var isl1 = GameCard.Create("Island");
        var isl2 = GameCard.Create("Island");
        state.Player2.Battlefield.Add(isl1);
        state.Player2.Battlefield.Add(isl2);
        var counter = GameCard.Create("Counterspell");
        state.Player2.Hand.Add(counter);

        // Step 1: P1 casts Goblin Guide
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mtn1.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        state.Stack.Should().HaveCount(1);

        // Step 2: P2 casts Counterspell targeting Goblin
        h2.EnqueueTarget(new TargetInfo(goblin.Id, state.Player1.Id, ZoneType.Stack));
        await engine.ExecuteAction(GameAction.TapCard(state.Player2.Id, isl1.Id));
        await engine.ExecuteAction(GameAction.TapCard(state.Player2.Id, isl2.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player2.Id, counter.Id));
        state.Stack.Should().HaveCount(2);

        // Step 3: P1 casts Bolt targeting P2
        h1.EnqueueTarget(new TargetInfo(Guid.Empty, state.Player2.Id, ZoneType.None));
        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mtn2.Id));
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, bolt.Id));
        state.Stack.Should().HaveCount(3);

        // Resolve stack (both auto-pass)
        await engine.RunPriorityAsync();

        // Bolt resolves: P2 takes 3 damage
        state.Player2.Life.Should().Be(17);

        // Counterspell resolves: Goblin is countered
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Goblin Guide");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "Counterspell");

        // Goblin Guide should NOT be on battlefield (was countered)
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Goblin Guide");
    }
}
