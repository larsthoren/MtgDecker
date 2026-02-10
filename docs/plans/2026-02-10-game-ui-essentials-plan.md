# Game UI Essentials Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add life counters (with custom delta, auto-lose at 0), library card count display, and exile zone to the game engine and Blazor UI.

**Architecture:** Three independent features that all touch the same files. Tasks are ordered so engine changes come first (with tests), then UI wiring. Life counters are the most complex (engine + session + UI with inline editing). Library count is display-only. Exile zone adds a new ZoneType enum value + zone on Player + UI display.

**Tech Stack:** .NET 10, MtgDecker.Engine (pure C#), Blazor InteractiveServer with MudBlazor, xUnit + FluentAssertions + NSubstitute.

**Environment:** Must run `export PATH="/c/Program Files/dotnet:$PATH"` before any dotnet commands. Run engine tests with `dotnet test tests/MtgDecker.Engine.Tests/`. Build web with `dotnet build src/MtgDecker.Web/`.

---

### Task 1: Add Exile zone to engine

Add `Exile` to `ZoneType` enum, add `Exile` zone to `Player`, update `GetZone`.

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/ZoneType.cs`
- Modify: `src/MtgDecker.Engine/Player.cs`
- Modify: `tests/MtgDecker.Engine.Tests/PlayerTests.cs`

**Step 1: Add failing test**

In `tests/MtgDecker.Engine.Tests/PlayerTests.cs`, update the existing `Constructor_InitializesEmptyZones` test to also check the Exile zone, and add `ZoneType.Exile` to the `GetZone_ReturnsCorrectZone` InlineData:

```csharp
// In Constructor_InitializesEmptyZones, add after the Graveyard assertions:
player.Exile.Type.Should().Be(ZoneType.Exile);
player.Exile.Count.Should().Be(0);

// Add new InlineData to GetZone_ReturnsCorrectZone:
[InlineData(ZoneType.Exile)]
```

**Step 2: Run test to verify it fails**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayerTests"
```

Expected: Compilation error — `ZoneType.Exile` doesn't exist, `Player.Exile` doesn't exist.

**Step 3: Implement**

In `src/MtgDecker.Engine/Enums/ZoneType.cs`, add `Exile` after `Graveyard`:

```csharp
namespace MtgDecker.Engine.Enums;

public enum ZoneType
{
    Library,
    Hand,
    Battlefield,
    Graveyard,
    Exile
}
```

In `src/MtgDecker.Engine/Player.cs`, add the Exile zone property, initialize it in the constructor, and add to GetZone:

```csharp
public Zone Exile { get; }

// In constructor, add:
Exile = new Zone(ZoneType.Exile);

// In GetZone, add before the default case:
ZoneType.Exile => Exile,
```

**Step 4: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayerTests"
```

Expected: All pass.

**Step 5: Run all engine tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/
```

Expected: All 140 pass (no regressions).

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/Enums/ZoneType.cs src/MtgDecker.Engine/Player.cs tests/MtgDecker.Engine.Tests/PlayerTests.cs
git commit -m "feat(engine): add Exile zone to Player and ZoneType enum"
```

---

### Task 2: Add Life property to Player

Add `int Life` property with default 20 to `Player`.

**Files:**
- Modify: `src/MtgDecker.Engine/Player.cs`
- Modify: `tests/MtgDecker.Engine.Tests/PlayerTests.cs`

**Step 1: Add failing test**

In `tests/MtgDecker.Engine.Tests/PlayerTests.cs`, add to `Constructor_SetsProperties`:

```csharp
player.Life.Should().Be(20);
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayerTests.Constructor_SetsProperties"
```

Expected: Compilation error — `Player.Life` doesn't exist.

**Step 3: Implement**

In `src/MtgDecker.Engine/Player.cs`, add after `public Zone Graveyard { get; }`:

```csharp
public int Life { get; set; } = 20;
```

**Step 4: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "PlayerTests"
```

Expected: All pass.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/Player.cs tests/MtgDecker.Engine.Tests/PlayerTests.cs
git commit -m "feat(engine): add Life property to Player with default 20"
```

---

### Task 3: Add AdjustLife to GameSession with auto-lose

Add `AdjustLife(int playerSeat, int delta)` method to `GameSession`. When life <= 0, auto-end the game.

**Files:**
- Modify: `src/MtgDecker.Engine/GameSession.cs`
- Modify: `tests/MtgDecker.Engine.Tests/GameSessionTests.cs`

**Step 1: Add failing tests**

In `tests/MtgDecker.Engine.Tests/GameSessionTests.cs`, add these tests:

```csharp
[Fact]
public async Task AdjustLife_ChangesPlayerLife()
{
    var session = new GameSession("ABC123");
    session.JoinPlayer("Alice", CreateDeck());
    session.JoinPlayer("Bob", CreateDeck());
    await session.StartAsync();

    session.AdjustLife(1, -3);

    session.State!.Player1.Life.Should().Be(17);
}

[Fact]
public async Task AdjustLife_CanAdjustOpponentLife()
{
    var session = new GameSession("ABC123");
    session.JoinPlayer("Alice", CreateDeck());
    session.JoinPlayer("Bob", CreateDeck());
    await session.StartAsync();

    session.AdjustLife(2, -5);

    session.State!.Player2.Life.Should().Be(15);
}

[Fact]
public async Task AdjustLife_AtZero_EndsGame()
{
    var session = new GameSession("ABC123");
    session.JoinPlayer("Alice", CreateDeck());
    session.JoinPlayer("Bob", CreateDeck());
    await session.StartAsync();

    session.AdjustLife(1, -20);

    session.State!.Player1.Life.Should().Be(0);
    session.IsGameOver.Should().BeTrue();
    session.Winner.Should().Be("Bob");
}

[Fact]
public async Task AdjustLife_BelowZero_EndsGame()
{
    var session = new GameSession("ABC123");
    session.JoinPlayer("Alice", CreateDeck());
    session.JoinPlayer("Bob", CreateDeck());
    await session.StartAsync();

    session.AdjustLife(2, -25);

    session.State!.Player2.Life.Should().BeLessThanOrEqualTo(0);
    session.IsGameOver.Should().BeTrue();
    session.Winner.Should().Be("Alice");
}

[Fact]
public async Task AdjustLife_LogsChange()
{
    var session = new GameSession("ABC123");
    session.JoinPlayer("Alice", CreateDeck());
    session.JoinPlayer("Bob", CreateDeck());
    await session.StartAsync();

    session.AdjustLife(1, -3);

    session.State!.GameLog.Should().Contain(l => l.Contains("20") && l.Contains("17"));
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameSessionTests.AdjustLife"
```

Expected: Compilation error — `AdjustLife` doesn't exist.

**Step 3: Implement**

In `src/MtgDecker.Engine/GameSession.cs`, add after the `Undo` method:

```csharp
public void AdjustLife(int playerSeat, int delta)
{
    if (State == null) return;
    var player = playerSeat == 1 ? State.Player1 : State.Player2;
    var oldLife = player.Life;
    player.Life += delta;
    State.Log($"{player.Name}'s life: {oldLife} → {player.Life}");

    if (player.Life <= 0)
    {
        State.IsGameOver = true;
        Winner = State.GetOpponent(player).Name;
        State.Log($"{player.Name} loses — life reached {player.Life}.");
        _cts?.Cancel();
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameSessionTests"
```

Expected: All pass.

**Step 5: Run all engine tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/
```

Expected: All pass.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameSession.cs tests/MtgDecker.Engine.Tests/GameSessionTests.cs
git commit -m "feat(engine): add AdjustLife to GameSession with auto-lose at 0"
```

---

### Task 4: Add life counter, library count, and exile to PlayerZone UI

Update the PlayerZone header to show life counter with +/- buttons, click-to-edit delta, library card count, and an exile zone display.

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor`

**Step 1: Add new parameters to PlayerZone**

In `PlayerZone.razor`, add these parameters to the `@code` section:

```csharp
[Parameter] public int Life { get; set; }
[Parameter] public int LibraryCount { get; set; }
[Parameter] public IReadOnlyList<GameCard> Exile { get; set; } = Array.Empty<GameCard>();
[Parameter] public EventCallback<int> OnLifeAdjust { get; set; }
```

**Step 2: Update the zone-header section**

Replace the existing `zone-header` div in `PlayerZone.razor` with:

```razor
<div class="zone-header">
    <MudText Typo="Typo.subtitle2">@PlayerName</MudText>

    @* Life counter *@
    <div style="display: flex; align-items: center; gap: 4px; margin-left: 12px;">
        <MudIconButton Icon="@Icons.Material.Filled.Remove" Size="Size.Small"
                       OnClick="() => OnLifeAdjust.InvokeAsync(-1)" />
        @if (_editingLife)
        {
            <MudTextField T="string" @bind-Value="_lifeDelta" Immediate="true"
                          Style="width: 60px;" Variant="Variant.Outlined" Margin="Margin.Dense"
                          OnKeyDown="HandleLifeKeyDown"
                          AutoFocus="true" />
        }
        else
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Error"
                     Style="cursor: pointer; min-width: 40px; justify-content: center;"
                     OnClick="StartEditLife">@Life</MudChip>
        }
        <MudIconButton Icon="@Icons.Material.Filled.Add" Size="Size.Small"
                       OnClick="() => OnLifeAdjust.InvokeAsync(1)" />
    </div>

    @if (IsActivePlayer)
    {
        <MudChip T="string" Size="Size.Small" Color="Color.Primary">Active</MudChip>
    }

    <MudText Typo="Typo.caption" Class="ml-2" Style="opacity: 0.7;">
        Library: @LibraryCount
    </MudText>
</div>
```

**Step 3: Add life editing logic**

Add these fields and methods to the `@code` section in `PlayerZone.razor`:

```csharp
private bool _editingLife;
private string _lifeDelta = "";

private void StartEditLife()
{
    _editingLife = true;
    _lifeDelta = "";
}

private async Task HandleLifeKeyDown(KeyboardEventArgs e)
{
    if (e.Key == "Enter" && int.TryParse(_lifeDelta, out var delta))
    {
        await OnLifeAdjust.InvokeAsync(delta);
        _editingLife = false;
    }
    else if (e.Key == "Escape")
    {
        _editingLife = false;
    }
}
```

**Step 4: Add exile zone display**

In `PlayerZone.razor`, add after the graveyard zone div and before the action menu:

```razor
@* Exile *@
<div class="zone graveyard">
    <MudText Typo="Typo.caption">Exile (@Exile.Count)</MudText>
    @if (Exile.Count > 0)
    {
        <CardDisplay Name="@Exile[^1].Name"
                     ImageUrl="@Exile[^1].ImageUrl"
                     Selected="@(SelectedCard?.Id == Exile[^1].Id)"
                     OnClick="() => SelectCard(Exile[^1], ZoneType.Exile)" />
    }
</div>
```

**Step 5: Update GameBoard to pass new parameters**

In `GameBoard.razor`, add `OnLifeAdjust` parameter and callback:

```csharp
[Parameter] public EventCallback<(int seat, int delta)> OnLifeAdjust { get; set; }
```

Update both PlayerZone usages. For the opponent zone, add:

```razor
Life="@OpponentPlayer.Life"
LibraryCount="@OpponentPlayer.Library.Count"
Exile="@OpponentPlayer.Exile.Cards"
OnLifeAdjust="(delta) => OnLifeAdjust.InvokeAsync((OpponentSeat, delta))"
```

For the local player zone, add:

```razor
Life="@LocalPlayer.Life"
LibraryCount="@LocalPlayer.Library.Count"
Exile="@LocalPlayer.Exile.Cards"
OnLifeAdjust="(delta) => OnLifeAdjust.InvokeAsync((PlayerSeat, delta))"
```

Add a helper property in GameBoard's `@code`:

```csharp
private int OpponentSeat => PlayerSeat == 1 ? 2 : 1;
```

**Step 6: Update GamePage to wire AdjustLife**

In `GamePage.razor`, add the `OnLifeAdjust` parameter on the `<GameBoard>` tag:

```razor
OnLifeAdjust="HandleLifeAdjust"
```

Add the handler method in `@code`:

```csharp
private void HandleLifeAdjust((int seat, int delta) args)
{
    _session?.AdjustLife(args.seat, args.delta);
}
```

**Step 7: Build and verify**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build src/MtgDecker.Web/ --no-restore
```

Expected: Build succeeded, 0 errors.

**Step 8: Run all engine tests**

```bash
dotnet test tests/MtgDecker.Engine.Tests/
```

Expected: All pass.

**Step 9: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor src/MtgDecker.Web/Components/Pages/GamePage.razor
git commit -m "feat(web): add life counters, library count, and exile zone to game UI"
```

---

### Task 5: Manual smoke test

Start the app and verify all three features work:

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet run --project src/MtgDecker.Web/
```

**Verify:**
1. Navigate to http://localhost:5044/game/new
2. Create game with deck, join from second tab
3. After mulligans, verify:
   - Life shows as 20 for both players with +/- buttons
   - Click the life number, type `-3`, press Enter → life becomes 17
   - Library count shows correct number (deck size minus 7 hand cards)
   - Action menu on a card shows "Exile" in the "Move to" list
   - Moving a card to Exile shows it in the exile zone area
4. Set a player to 0 life → game should auto-end

No commit — this is verification only.
