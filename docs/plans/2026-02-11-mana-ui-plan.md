# Mana UI Integration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Wire the engine's mana system into the game UI so cards use registered definitions, mana pools are visible, and tapping lands for mana works interactively.

**Architecture:** Five changes across engine and web layers: (1) use `GameCard.Create()` for card resolution, (2) auto-pay generic costs in `InteractiveDecisionHandler`, (3) expose mana color options for UI, (4) display mana pools in PlayerZone, (5) smart battlefield click with inline color picker.

**Tech Stack:** .NET 10, Blazor (InteractiveServer), MudBlazor 8.x, existing Engine mana types.

---

### Task 1: Use GameCard.Create() in GameLobby

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/GameLobby.razor:176`

**Step 1:** Change card creation to use the factory method.

Replace lines 176–181:
```csharp
                gameCards.Add(new GameCard
                {
                    Name = card.Name,
                    TypeLine = card.TypeLine,
                    ImageUrl = card.ImageUriSmall ?? card.ImageUri
                });
```

With:
```csharp
                gameCards.Add(GameCard.Create(card.Name, card.TypeLine, card.ImageUriSmall ?? card.ImageUri));
```

**Step 2:** Verify it compiles.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build src/MtgDecker.Web/
```

Expected: `Build succeeded. 0 Error(s)`

**Step 3:** Commit.

```bash
git add src/MtgDecker.Web/Components/Pages/GameLobby.razor
git commit -m "fix(web): use GameCard.Create() so cards get mana definitions"
```

---

### Task 2: Auto-Pay Generic Costs in InteractiveDecisionHandler

**Files:**
- Test: `tests/MtgDecker.Engine.Tests/InteractiveDecisionHandlerTests.cs` (create)
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs:58-65`

**Step 1:** Write the failing test.

Create `tests/MtgDecker.Engine.Tests/InteractiveDecisionHandlerTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class InteractiveDecisionHandlerTests
{
    [Fact]
    public async Task ChooseGenericPayment_AutoPays_FromLargestPoolFirst()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 1 },
            { ManaColor.Green, 3 }
        };

        var result = await handler.ChooseGenericPayment(2, available);

        result[ManaColor.Green].Should().Be(2);
        result.Should().NotContainKey(ManaColor.Red);
    }

    [Fact]
    public async Task ChooseGenericPayment_AutoPays_SplitsAcrossColors()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 1 },
            { ManaColor.Green, 1 }
        };

        var result = await handler.ChooseGenericPayment(2, available);

        result.Values.Sum().Should().Be(2);
    }

    [Fact]
    public async Task ChooseGenericPayment_DoesNotBlockOnTaskCompletionSource()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 }
        };

        // Should complete immediately, not block waiting for UI input
        var task = handler.ChooseGenericPayment(1, available);
        task.IsCompleted.Should().BeTrue();

        var result = await task;
        result[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task ChooseGenericPayment_IsNotWaitingAfterCall()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 }
        };

        await handler.ChooseGenericPayment(1, available);

        handler.IsWaitingForGenericPayment.Should().BeFalse();
    }
}
```

**Step 2:** Run test to verify it fails.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "InteractiveDecisionHandlerTests" --no-restore
```

Expected: FAIL — current implementation creates a TaskCompletionSource and blocks.

**Step 3:** Implement auto-pay.

In `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`, replace the `ChooseGenericPayment` method (lines 58–65):

```csharp
    public Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available, CancellationToken ct = default)
    {
        var payment = new Dictionary<ManaColor, int>();
        var remaining = genericAmount;
        foreach (var (color, amount) in available.OrderByDescending(kv => kv.Value))
        {
            if (remaining <= 0) break;
            var take = Math.Min(amount, remaining);
            payment[color] = take;
            remaining -= take;
        }
        return Task.FromResult(payment);
    }
```

**Step 4:** Run tests to verify they pass.

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "InteractiveDecisionHandlerTests" --no-restore
```

Expected: `Passed! 4 passed`

**Step 5:** Run all engine tests for regression.

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --no-restore
```

Expected: All 271 pass.

**Step 6:** Commit.

```bash
git add tests/MtgDecker.Engine.Tests/InteractiveDecisionHandlerTests.cs src/MtgDecker.Engine/InteractiveDecisionHandler.cs
git commit -m "feat(engine): auto-pay generic mana costs in InteractiveDecisionHandler"
```

---

### Task 3: Expose ManaColorOptions on InteractiveDecisionHandler

**Files:**
- Modify: `tests/MtgDecker.Engine.Tests/InteractiveDecisionHandlerTests.cs`
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`

**Step 1:** Write the failing tests.

Add to `InteractiveDecisionHandlerTests.cs`:

```csharp
    [Fact]
    public async Task ChooseManaColor_ExposesManaColorOptions()
    {
        var handler = new InteractiveDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };

        // Start the choice (will block until resolved)
        var task = handler.ChooseManaColor(options);

        handler.IsWaitingForManaColor.Should().BeTrue();
        handler.ManaColorOptions.Should().BeEquivalentTo(options);

        // Resolve it
        handler.SubmitManaColor(ManaColor.Red);
        var result = await task;
        result.Should().Be(ManaColor.Red);
    }

    [Fact]
    public async Task ChooseManaColor_ClearsOptionsAfterSubmission()
    {
        var handler = new InteractiveDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };

        var task = handler.ChooseManaColor(options);
        handler.SubmitManaColor(ManaColor.Green);
        await task;

        handler.ManaColorOptions.Should().BeNull();
    }
```

**Step 2:** Run tests to verify they fail.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "ManaColorOptions" --no-restore
```

Expected: FAIL — `ManaColorOptions` property doesn't exist.

**Step 3:** Implement.

In `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`:

Add the property near the other `IsWaiting*` properties (around line 18):

```csharp
    public IReadOnlyList<ManaColor>? ManaColorOptions { get; private set; }
```

In the `ChooseManaColor` method, store the options (after creating the TCS, before returning):

```csharp
    public Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default)
    {
        ManaColorOptions = options;
        _manaColorTcs = new TaskCompletionSource<ManaColor>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _manaColorTcs.TrySetCanceled());
        _manaColorTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _manaColorTcs.Task;
    }
```

In the `SubmitManaColor` method, clear the options:

```csharp
    public void SubmitManaColor(ManaColor color)
    {
        ManaColorOptions = null;
        _manaColorTcs?.TrySetResult(color);
    }
```

**Step 4:** Run tests.

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "InteractiveDecisionHandlerTests" --no-restore
```

Expected: All 6 pass.

**Step 5:** Run all engine tests.

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --no-restore
```

Expected: All 271 pass.

**Step 6:** Commit.

```bash
git add tests/MtgDecker.Engine.Tests/InteractiveDecisionHandlerTests.cs src/MtgDecker.Engine/InteractiveDecisionHandler.cs
git commit -m "feat(engine): expose ManaColorOptions on InteractiveDecisionHandler"
```

---

### Task 4: Mana Pool Display in PlayerZone

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1:** Add the mana pool rendering to `PlayerZone.razor`.

Add `using MtgDecker.Engine.Mana` and `using MtgDecker.Engine.Enums` imports (Enums already present, add Mana):

After line 2 (`@using MtgDecker.Engine.Enums`), add:

```razor
@using MtgDecker.Engine.Mana
```

Add a new parameter in the `@code` block after `OnAction` (line 150):

```csharp
    [Parameter] public ManaPool? ManaPool { get; set; }
```

Add a helper method to render mana symbols, at the end of the `@code` block:

```csharp
    private static readonly (ManaColor color, string symbol)[] ManaOrder = new[]
    {
        (ManaColor.White, "W"),
        (ManaColor.Blue, "U"),
        (ManaColor.Black, "B"),
        (ManaColor.Red, "R"),
        (ManaColor.Green, "G"),
        (ManaColor.Colorless, "C")
    };
```

Add the mana pool display in the zone header, after the draw card button (after line 43, before the closing `</div>` of zone-header):

```razor
        @if (ManaPool != null && ManaPool.Total > 0)
        {
            <div class="mana-pool-display">
                @foreach (var (color, symbol) in ManaOrder)
                {
                    @if (ManaPool[color] > 0)
                    {
                        <span class="mana-entry">
                            <img src="https://svgs.scryfall.io/card-symbols/@(symbol).svg"
                                 alt="@color" class="mana-symbol" />
                            <span class="mana-count">@ManaPool[color]</span>
                        </span>
                    }
                }
            </div>
        }
```

**Step 2:** Add styles to `PlayerZone.razor.css`.

Append to the file:

```css
.mana-pool-display {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-left: 8px;
    padding: 2px 8px;
    border-radius: 4px;
    background: rgba(255, 255, 255, 0.05);
}

.mana-entry {
    display: inline-flex;
    align-items: center;
    gap: 2px;
}

.mana-symbol {
    width: 20px;
    height: 20px;
}

.mana-count {
    font-size: 0.85rem;
    font-weight: 600;
}
```

**Step 3:** Pass ManaPool from GameBoard to PlayerZone.

In `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`, add `ManaPool` to both PlayerZone instances.

For the opponent zone (around line 29), add after `Exile`:

```razor
                    ManaPool="@OpponentPlayer.ManaPool"
```

For the local zone (around line 72), add after `Exile`:

```razor
                    ManaPool="@LocalPlayer.ManaPool"
```

**Step 4:** Verify it compiles.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build src/MtgDecker.Web/
```

Expected: `Build succeeded. 0 Error(s)`

**Step 5:** Commit.

```bash
git add src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor
git commit -m "feat(web): display mana pool with Scryfall SVG symbols in PlayerZone"
```

---

### Task 5: Smart Battlefield Click — Fast-Tap and Mana Color Picker

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor`

**Step 1:** Add mana choice parameters to PlayerZone.

In `PlayerZone.razor` `@code` block, add new parameters after the `ManaPool` parameter:

```csharp
    [Parameter] public bool IsWaitingForManaColor { get; set; }
    [Parameter] public IReadOnlyList<ManaColor>? ManaColorOptions { get; set; }
    [Parameter] public EventCallback<ManaColor> OnManaColorChosen { get; set; }
```

**Step 2:** Change battlefield card click behavior.

Replace the `SelectCard` method with smart-tap logic:

```csharp
    private async Task SelectCard(GameCard card, ZoneType zone)
    {
        if (!CanAct && !IsOpponent) return;

        if (zone == ZoneType.Battlefield && !IsOpponent && !card.IsTapped && card.IsLand)
        {
            // Fast-tap: untapped land with mana ability
            if (card.ManaAbility != null)
            {
                // Dispatch TapCard immediately — for fixed abilities this resolves instantly,
                // for choice abilities the engine will block and color picker appears
                await OnAction.InvokeAsync(GameAction.TapCard(PlayerId, card.Id));
                ClearSelection();
                return;
            }
        }

        // Default: toggle action menu
        if (SelectedCard?.Id == card.Id)
        {
            ClearSelection();
            return;
        }
        SelectedCard = card;
        _selectedZone = zone;
    }
```

Note: `SelectCard` was previously `void`, now it's `async Task`. Update the `OnClick` lambdas in the battlefield and hand sections. In the battlefield section (line 55), change:

```razor
OnClick="() => SelectCard(card, ZoneType.Battlefield)"
```

to:

```razor
OnClick="async () => await SelectCard(card, ZoneType.Battlefield)"
```

In the hand section for opponent (line 72):

```razor
OnClick="async () => await SelectCard(Hand[index], ZoneType.Hand)"
```

In the hand section for local player (line 82):

```razor
OnClick="async () => await SelectCard(card, ZoneType.Hand)"
```

In the graveyard section (line 103):

```razor
OnClick="async () => await SelectCard(Graveyard[^1], ZoneType.Graveyard)"
```

In the exile section (line 115):

```razor
OnClick="async () => await SelectCard(Exile[^1], ZoneType.Exile)"
```

**Step 3:** Add the inline mana color picker.

In `PlayerZone.razor`, after the existing action menu block (after the `@if (SelectedCard != null)` block, around line 133), add:

```razor
    @if (IsWaitingForManaColor && ManaColorOptions != null && !IsOpponent)
    {
        <div class="mana-color-picker">
            <MudText Typo="Typo.caption" Class="mb-1">Choose mana color:</MudText>
            <div class="mana-choice-buttons">
                @foreach (var color in ManaColorOptions)
                {
                    var symbol = ManaOrder.First(m => m.color == color).symbol;
                    <MudIconButton Class="mana-choice-btn"
                                   OnClick="async () => await OnManaColorChosen.InvokeAsync(color)">
                        <img src="https://svgs.scryfall.io/card-symbols/@(symbol).svg"
                             alt="@color" style="width: 32px; height: 32px;" />
                    </MudIconButton>
                }
                <MudButton Size="Size.Small" Variant="Variant.Text" Color="Color.Default"
                           OnClick="ClearSelection">Cancel</MudButton>
            </div>
        </div>
    }
```

**Step 4:** Add styles for the color picker to `PlayerZone.razor.css`:

```css
.mana-color-picker {
    display: inline-block;
    padding: 8px 12px;
    border-radius: 8px;
    background: rgba(255, 255, 255, 0.08);
    border: 1px solid rgba(255, 255, 255, 0.15);
    margin: 4px;
}

.mana-choice-buttons {
    display: flex;
    align-items: center;
    gap: 8px;
}

.mana-choice-btn {
    padding: 4px !important;
    min-width: 40px !important;
    border-radius: 50% !important;
}

.mana-choice-btn:hover {
    background: rgba(255, 255, 255, 0.15) !important;
}
```

**Step 5:** Wire up GameBoard to pass mana choice state.

In `GameBoard.razor`, add parameters for mana choice state. Add to the `@code` block:

```csharp
    private bool IsWaitingForManaColor => Handler?.IsWaitingForManaColor == true;
    private IReadOnlyList<ManaColor>? ManaColorOptions => Handler?.ManaColorOptions;
```

Pass these to the local PlayerZone (not the opponent zone). Add to the local PlayerZone tag (around line 66):

```razor
                    IsWaitingForManaColor="@IsWaitingForManaColor"
                    ManaColorOptions="@ManaColorOptions"
                    OnManaColorChosen="HandleManaColorChosen"
```

Add the handler method in the `@code` block:

```csharp
    [Parameter] public EventCallback<ManaColor> OnManaColorChosen { get; set; }

    private async Task HandleManaColorChosen(ManaColor color)
    {
        await OnManaColorChosen.InvokeAsync(color);
    }
```

**Step 6:** Wire up GamePage to submit mana color choice.

In `GamePage.razor`, add the `OnManaColorChosen` parameter to the GameBoard tag (around line 33):

```razor
               OnManaColorChosen="HandleManaColorChosen"
```

Add the handler method in `@code`:

```csharp
    private void HandleManaColorChosen(ManaColor color)
    {
        var handler = _session?.GetHandler(_playerSeat);
        handler?.SubmitManaColor(color);
    }
```

Also update `HandleWaitingForInput` to trigger StateHasChanged for mana color prompts. After the existing `else if (handler.IsWaitingForAction)` block (line 97-100), add:

```csharp
        else if (handler.IsWaitingForManaColor)
        {
            InvokeAsync(StateHasChanged);
        }
```

Add the `ManaColor` using if not present at the top:

```razor
@using MtgDecker.Engine.Enums
```

**Step 7:** Verify it compiles.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build src/MtgDecker.Web/
```

Expected: `Build succeeded. 0 Error(s)`

**Step 8:** Run all tests for regression.

```bash
dotnet test tests/MtgDecker.Engine.Tests/ --no-restore
```

Expected: All tests pass.

**Step 9:** Commit.

```bash
git add src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor src/MtgDecker.Web/Components/Pages/GamePage.razor
git commit -m "feat(web): smart land tapping and inline mana color picker"
```

---

### Task 6: Manual Smoke Test

**No code changes.** Verify end-to-end manually:

1. Start the app: `dotnet run --project src/MtgDecker.Web/`
2. Create a new game with the Legacy Goblins deck.
3. Join as second player with the Legacy Enchantress deck.
4. Verify: cards now have mana costs (not sandbox mode).
5. Verify: clicking an untapped Mountain instantly taps it and shows Red mana in the pool.
6. Verify: mana pool shows Scryfall SVG symbols with counts in the header.
7. Verify: clicking a dual land (e.g., Karplusan Forest) shows the color picker.
8. Verify: selecting a color from the picker taps the land and adds the chosen mana.
9. Verify: casting a creature with sufficient mana deducts from the pool.
10. Verify: casting with insufficient mana is rejected (card stays in hand).
11. Verify: undo after tapping removes mana from pool.
12. Verify: opponent's mana pool is also visible.
