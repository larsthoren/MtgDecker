# Ninjutsu UI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add UI for Ninjutsu activation — click ninja in hand, click "Ninjutsu" button, click unblocked attacker to complete.

**Architecture:** Extends ActionMenu.razor with a new "Ninjutsu" button and GameBoard.razor with a ninjutsu targeting mode (similar to the existing spell-targeting pattern). No engine changes needed — backend is complete.

**Tech Stack:** Blazor, MudBlazor, C# 14

---

### Task 1: Add Ninjutsu button to ActionMenu

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/ActionMenu.razor`

**Step 1: Add parameters and button**

Add two new parameters and a "Ninjutsu" button to ActionMenu.razor. The button appears when the card is in hand and has a ninjutsu cost.

Add parameters after `HasAlternateCost` (line 64):
```csharp
[Parameter] public bool HasNinjutsuCost { get; set; }
[Parameter] public EventCallback OnNinjutsu { get; set; }
```

Add the Ninjutsu button markup in the `MudStack` after the alternate cost button block (after line 29), before the `MudDivider`:
```razor
@if (IsOwnCard && CurrentZone == ZoneType.Hand && HasNinjutsuCost)
{
    <MudButton Size="Size.Small" Variant="Variant.Text" FullWidth="true"
               StartIcon="@Icons.Material.Filled.SwapHoriz" Color="Color.Secondary"
               Style="justify-content: flex-start;"
               OnClick="() => OnNinjutsu.InvokeAsync()">Ninjutsu</MudButton>
}
```

**Step 2: Build to verify**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/ActionMenu.razor
git commit -m "feat(web): add Ninjutsu button to ActionMenu component"
```

---

### Task 2: Add ninjutsu state and helper methods to GameBoard

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1: Add ninjutsu targeting state field**

Add after the `_pendingCastCard` field (line 858):
```csharp
// Ninjutsu targeting state (waiting for player to click an unblocked attacker)
private GameCard? _ninjutsuCard;
```

**Step 2: Add IsNinjutsuTargeting property**

Add near the other targeting helpers (after `IsTargetingDimmed` around line 918):
```csharp
// --- Ninjutsu targeting helpers ---
private bool IsNinjutsuTargeting => _ninjutsuCard != null;
```

**Step 3: Add HasNinjutsuAvailable method**

Add in the helpers section (after `HasAlternateCostForCard` around line 1352):
```csharp
// --- Ninjutsu ---

private bool HasNinjutsuAvailable(GameCard? card)
{
    if (card == null) return false;
    if (!CardDefinitions.TryGet(card.Name, out var def) || def.NinjutsuCost == null)
        return false;
    if (State.CurrentPhase != Phase.Combat || State.CombatStep < CombatStep.DeclareBlockers)
        return false;
    if (State.Combat == null) return false;
    // Must have at least one unblocked attacker we control
    return State.Combat.Attackers.Any(a =>
        !State.Combat.IsBlocked(a)
        && LocalPlayer.Battlefield.Cards.Any(c => c.Id == a));
}

private bool IsEligibleNinjutsuTarget(GameCard card)
{
    if (!IsNinjutsuTargeting || State.Combat == null) return false;
    return State.Combat.Attackers.Contains(card.Id)
        && !State.Combat.IsBlocked(card.Id);
}

private bool IsNinjutsuDimmed(GameCard card)
{
    return IsNinjutsuTargeting && !IsEligibleNinjutsuTarget(card);
}
```

**Step 4: Build to verify**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor
git commit -m "feat(web): add ninjutsu targeting state and helper methods"
```

---

### Task 3: Add ninjutsu action handlers to GameBoard

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1: Add HandleNinjutsu method**

Add after the `HandlePlayAlternate` method (around line 1359):
```csharp
// --- Ninjutsu ---

private void HandleNinjutsu()
{
    if (_selectedCard == null) return;
    _ninjutsuCard = _selectedCard;
    ClearSelection();
}

private async Task HandleNinjutsuTargetClick(GameCard card)
{
    if (_ninjutsuCard == null) return;
    await OnAction.InvokeAsync(
        GameAction.Ninjutsu(LocalPlayer.Id, _ninjutsuCard.Id, card.Id));
    _ninjutsuCard = null;
}

private void CancelNinjutsu() => _ninjutsuCard = null;
```

**Step 2: Build to verify**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor
git commit -m "feat(web): add ninjutsu action handlers"
```

---

### Task 4: Wire ActionMenu ninjutsu parameters

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1: Wire HasNinjutsuCost and OnNinjutsu on the ActionMenu**

In the ActionMenu rendering block (around lines 441-458), add two new parameters:
```razor
HasNinjutsuCost="@HasNinjutsuAvailable(_selectedCard)"
OnNinjutsu="HandleNinjutsu"
```

The full ActionMenu block should become:
```razor
@if (_selectedCard != null && HasPriority)
{
    <div class="prompt-overlay">
        <ActionMenu Visible="true"
                    CardName="@_selectedCard.Name"
                    CurrentZone="_selectedZone"
                    IsOwnCard="true"
                    IsTapped="@_selectedCard.IsTapped"
                    HasTapAbility="@(_selectedCard.IsLand || _selectedCard.ManaAbility != null)"
                    HasActivatedAbility="@HasActivatedAbility(_selectedCard)"
                    HasAlternateCost="@HasAlternateCostForCard(_selectedCard)"
                    HasNinjutsuCost="@HasNinjutsuAvailable(_selectedCard)"
                    OnPlay="HandlePlay"
                    OnPlayAlternate="HandlePlayAlternate"
                    OnTapToggle="HandleTapToggle"
                    OnActivate="HandleActivate"
                    OnNinjutsu="HandleNinjutsu"
                    OnClose="ClearSelection" />
    </div>
}
```

**Step 2: Build to verify**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor
git commit -m "feat(web): wire ninjutsu parameters on ActionMenu"
```

---

### Task 5: Add ninjutsu targeting banner and modify battlefield click handlers

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1: Add ninjutsu targeting banner**

Add a banner for ninjutsu targeting mode, right after the existing `IsTargetingMode` banner block (after line 179). This follows the same pattern:
```razor
@if (IsNinjutsuTargeting)
{
    <div class="targeting-banner">
        <span>Choose unblocked attacker to return to hand</span>
        <MudButton Size="Size.Small" Variant="Variant.Outlined" Color="Color.Warning"
                   OnClick="CancelNinjutsu">Cancel</MudButton>
    </div>
}
```

**Step 2: Intercept battlefield clicks during ninjutsu targeting**

In `HandleBattlefieldClick` (around line 977), add ninjutsu targeting interception at the top of the method, before the existing targeting check:
```csharp
// If ninjutsu targeting, check if this is a valid unblocked attacker
if (IsNinjutsuTargeting)
{
    if (IsEligibleNinjutsuTarget(card))
        await HandleNinjutsuTargetClick(card);
    return;
}
```

**Step 3: Add visual dimming/highlighting during ninjutsu mode**

In the player creatures section (around line 211), the existing `CardDisplay` for player battlefield creatures needs to also factor in ninjutsu targeting state. Find the player creature `CardDisplay` rendering. Look for the `class` on the `<div>` wrapper. We need to add eligible/dimmed styling.

In the player creatures loop (around lines 211-250), find where the local player's creatures are rendered. The `CardDisplay` should get additional parameters or the wrapping div should get CSS classes for ninjutsu targeting:

Locate the `<div>` wrapping each creature permanent. Add a CSS class condition:
```razor
<div class="permanent-stack @(IsEligibleNinjutsuTarget(card) ? "ninjutsu-eligible" : "") @(IsNinjutsuDimmed(card) ? "ninjutsu-dimmed" : "")">
```

**Step 4: Add CSS for ninjutsu targeting**

In the `<style>` section at the bottom of GameBoard.razor, add:
```css
.ninjutsu-eligible {
    outline: 2px solid var(--mud-palette-secondary);
    border-radius: 8px;
    cursor: pointer;
}

.ninjutsu-dimmed {
    opacity: 0.35;
    pointer-events: none;
}
```

**Step 5: Build to verify**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor
git commit -m "feat(web): add ninjutsu targeting banner, click handlers, and visual feedback"
```

---

### Task 6: Run all tests and verify build

**Files:**
- None (verification only)

**Step 1: Build the whole project**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeded

**Step 2: Run engine tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All tests pass (no engine changes, but verify no regressions)

**Step 3: Commit if any fixes were needed**

If any adjustments were required, commit them.
