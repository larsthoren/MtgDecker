# Game UI Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Redesign the game UI to an MTGO-inspired layout with phase bar, phase stops, click-to-attack combat, land/creature row separation, and slide-over game log.

**Architecture:** Restructure the Blazor game components from a two-column layout (board + log sidebar) to a full-viewport CSS grid. Add a new PhaseBar component with stop toggles. Refactor PlayerZone into compact OpponentZone and full-featured PlayerZone. Engine changes limited to a new PhaseStopSettings class and auto-pass logic in InteractiveDecisionHandler.

**Tech Stack:** Blazor (.razor + scoped CSS), MudBlazor 8.x, C# 14, CSS Grid

---

### Task 1: PhaseStopSettings (Engine — TDD)

**Files:**
- Create: `src/MtgDecker.Engine/PhaseStopSettings.cs`
- Test: `tests/MtgDecker.Engine.Tests/PhaseStopSettingsTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Engine.Tests/PhaseStopSettingsTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class PhaseStopSettingsTests
{
    [Fact]
    public void DefaultStops_IncludeMainPhasesAndCombat()
    {
        var settings = new PhaseStopSettings();

        settings.ShouldStop(Phase.MainPhase1).Should().BeTrue();
        settings.ShouldStop(Phase.MainPhase2).Should().BeTrue();
        settings.ShouldStop(CombatStep.DeclareAttackers).Should().BeTrue();
        settings.ShouldStop(CombatStep.DeclareBlockers).Should().BeTrue();
    }

    [Fact]
    public void DefaultStops_ExcludeNonMainPhases()
    {
        var settings = new PhaseStopSettings();

        settings.ShouldStop(Phase.Untap).Should().BeFalse();
        settings.ShouldStop(Phase.Upkeep).Should().BeFalse();
        settings.ShouldStop(Phase.Draw).Should().BeFalse();
        settings.ShouldStop(Phase.End).Should().BeFalse();
        settings.ShouldStop(CombatStep.BeginCombat).Should().BeFalse();
        settings.ShouldStop(CombatStep.CombatDamage).Should().BeFalse();
        settings.ShouldStop(CombatStep.EndCombat).Should().BeFalse();
    }

    [Fact]
    public void TogglePhase_TogglesOnAndOff()
    {
        var settings = new PhaseStopSettings();

        settings.TogglePhase(Phase.Upkeep);
        settings.ShouldStop(Phase.Upkeep).Should().BeTrue();

        settings.TogglePhase(Phase.Upkeep);
        settings.ShouldStop(Phase.Upkeep).Should().BeFalse();
    }

    [Fact]
    public void ToggleCombatStep_TogglesOnAndOff()
    {
        var settings = new PhaseStopSettings();

        settings.ToggleCombatStep(CombatStep.BeginCombat);
        settings.ShouldStop(CombatStep.BeginCombat).Should().BeTrue();

        settings.ToggleCombatStep(CombatStep.BeginCombat);
        settings.ShouldStop(CombatStep.BeginCombat).Should().BeFalse();
    }

    [Fact]
    public void ShouldStop_Phase_IgnoresCombatPhase()
    {
        // Phase.Combat is handled via CombatStep, not the Phase enum
        var settings = new PhaseStopSettings();
        settings.ShouldStop(Phase.Combat).Should().BeFalse();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "PhaseStopSettings" -v minimal`
Expected: Build error — `PhaseStopSettings` doesn't exist

**Step 3: Write minimal implementation**

```csharp
// src/MtgDecker.Engine/PhaseStopSettings.cs
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public class PhaseStopSettings
{
    public HashSet<Phase> PhaseStops { get; } = [Phase.MainPhase1, Phase.MainPhase2];
    public HashSet<CombatStep> CombatStops { get; } = [CombatStep.DeclareAttackers, CombatStep.DeclareBlockers];

    public bool ShouldStop(Phase phase) => PhaseStops.Contains(phase);
    public bool ShouldStop(CombatStep step) => CombatStops.Contains(step);

    public void TogglePhase(Phase phase)
    {
        if (!PhaseStops.Remove(phase))
            PhaseStops.Add(phase);
    }

    public void ToggleCombatStep(CombatStep step)
    {
        if (!CombatStops.Remove(step))
            CombatStops.Add(step);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "PhaseStopSettings" -v minimal`
Expected: 5 tests pass

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/PhaseStopSettings.cs tests/MtgDecker.Engine.Tests/PhaseStopSettingsTests.cs
git commit -m "feat(engine): add PhaseStopSettings with default stops for main phases and combat"
```

---

### Task 2: Wire PhaseStopSettings into InteractiveDecisionHandler

**Files:**
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`
- Test: `tests/MtgDecker.Engine.Tests/PhaseStopSettingsTests.cs` (add more tests)

**Step 1: Add auto-pass tests**

Add to `PhaseStopSettingsTests.cs`:

```csharp
[Fact]
public async Task InteractiveHandler_AutoPasses_WhenNoStop()
{
    var handler = new InteractiveDecisionHandler();
    handler.PhaseStops.TogglePhase(Phase.Upkeep); // enable upkeep stop

    // When no stop is set for Draw, ShouldAutoPass returns true
    handler.ShouldAutoPass(Phase.Draw, CombatStep.None, stackEmpty: true).Should().BeTrue();
}

[Fact]
public async Task InteractiveHandler_DoesNotAutoPass_WhenStopSet()
{
    var handler = new InteractiveDecisionHandler();

    handler.ShouldAutoPass(Phase.MainPhase1, CombatStep.None, stackEmpty: true).Should().BeFalse();
}

[Fact]
public async Task InteractiveHandler_DoesNotAutoPass_WhenStackHasItems()
{
    var handler = new InteractiveDecisionHandler();

    // Even if no stop, if stack has items, don't auto-pass
    handler.ShouldAutoPass(Phase.Draw, CombatStep.None, stackEmpty: false).Should().BeFalse();
}

[Fact]
public async Task InteractiveHandler_ChecksCombatStops()
{
    var handler = new InteractiveDecisionHandler();

    handler.ShouldAutoPass(Phase.Combat, CombatStep.DeclareAttackers, stackEmpty: true).Should().BeFalse();
    handler.ShouldAutoPass(Phase.Combat, CombatStep.CombatDamage, stackEmpty: true).Should().BeTrue();
}
```

**Step 2: Run tests to verify they fail**

Expected: Build error — `PhaseStops` and `ShouldAutoPass` don't exist on handler

**Step 3: Add PhaseStopSettings and ShouldAutoPass to InteractiveDecisionHandler**

Add to `InteractiveDecisionHandler.cs`:

```csharp
public PhaseStopSettings PhaseStops { get; } = new();

public bool ShouldAutoPass(Phase phase, CombatStep combatStep, bool stackEmpty)
{
    // Always stop if stack has items
    if (!stackEmpty) return false;

    // During combat, check combat step stops
    if (phase == Phase.Combat && combatStep != CombatStep.None)
        return !PhaseStops.ShouldStop(combatStep);

    // Otherwise check phase stops
    return !PhaseStops.ShouldStop(phase);
}
```

**Step 4: Run tests to verify they pass**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ --filter "PhaseStopSettings" -v minimal`
Expected: 9 tests pass

**Step 5: Run full engine tests to verify no regressions**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v minimal`
Expected: All 817+ tests pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/InteractiveDecisionHandler.cs tests/MtgDecker.Engine.Tests/PhaseStopSettingsTests.cs
git commit -m "feat(engine): wire PhaseStopSettings into InteractiveDecisionHandler with auto-pass logic"
```

---

### Task 3: PhaseBar Component

**Files:**
- Create: `src/MtgDecker.Web/Components/Pages/Game/PhaseBar.razor`
- Create: `src/MtgDecker.Web/Components/Pages/Game/PhaseBar.razor.css`

This is a Blazor component — no TDD, test via visual inspection.

**Step 1: Create PhaseBar.razor**

The component renders all 11 turn steps as a horizontal strip. Each step is clickable to toggle its phase stop. The active phase is highlighted.

```razor
@using MtgDecker.Engine
@using MtgDecker.Engine.Enums
@namespace MtgDecker.Web.Components.Pages.Game

<div class="phase-bar">
    <div class="phase-steps">
        @foreach (var step in _steps)
        {
            var isActive = IsStepActive(step);
            var isPast = IsStepPast(step);
            var hasStop = HasStop(step);
            <div class="phase-step @(isActive ? "active" : "") @(isPast ? "past" : "") @(hasStop ? "has-stop" : "")"
                 @onclick="() => ToggleStop(step)">
                <span class="step-label">@step.Label</span>
                <span class="stop-dot">@(hasStop ? "●" : "○")</span>
            </div>
        }
    </div>

    <div class="phase-actions">
        @if (ShowStackInfo && StackCount > 0)
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Secondary">
                Stack: @StackCount
            </MudChip>
        }

        @if (HasPriority)
        {
            <MudButton Size="Size.Small" Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="OnPass">
                @PassButtonLabel
            </MudButton>
        }
        else if (!IsWaitingForCombat)
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Default">Waiting...</MudChip>
        }

        <MudIconButton Size="Size.Small" Icon="@Icons.Material.Filled.Undo"
                       Color="Color.Warning" Variant="Variant.Outlined"
                       Disabled="@(!CanUndo)"
                       OnClick="OnUndo" />
        <MudIconButton Size="Size.Small" Icon="@Icons.Material.Filled.Flag"
                       Color="Color.Error" Variant="Variant.Outlined"
                       OnClick="OnSurrender" />
    </div>
</div>

@code {
    [Parameter] public Phase CurrentPhase { get; set; }
    [Parameter] public CombatStep CurrentCombatStep { get; set; }
    [Parameter] public int TurnNumber { get; set; }
    [Parameter] public string ActivePlayerName { get; set; } = "";
    [Parameter] public bool HasPriority { get; set; }
    [Parameter] public bool IsWaitingForCombat { get; set; }
    [Parameter] public bool IsWaitingForAttackers { get; set; }
    [Parameter] public bool IsWaitingForBlockers { get; set; }
    [Parameter] public bool CanUndo { get; set; }
    [Parameter] public int StackCount { get; set; }
    [Parameter] public bool ShowStackInfo { get; set; } = true;
    [Parameter] public PhaseStopSettings? StopSettings { get; set; }
    [Parameter] public EventCallback OnPass { get; set; }
    [Parameter] public EventCallback OnUndo { get; set; }
    [Parameter] public EventCallback OnSurrender { get; set; }

    private string PassButtonLabel => IsWaitingForAttackers ? "Confirm Attacks"
        : IsWaitingForBlockers ? "Confirm Blocks"
        : "Pass";

    private record StepInfo(string Label, Phase Phase, CombatStep CombatStep);

    private static readonly StepInfo[] _steps =
    [
        new("Untap", Phase.Untap, CombatStep.None),
        new("Upkeep", Phase.Upkeep, CombatStep.None),
        new("Draw", Phase.Draw, CombatStep.None),
        new("Main", Phase.MainPhase1, CombatStep.None),
        new("Begin Cbt", Phase.Combat, CombatStep.BeginCombat),
        new("Attackers", Phase.Combat, CombatStep.DeclareAttackers),
        new("Blockers", Phase.Combat, CombatStep.DeclareBlockers),
        new("Damage", Phase.Combat, CombatStep.CombatDamage),
        new("End Cbt", Phase.Combat, CombatStep.EndCombat),
        new("Main", Phase.MainPhase2, CombatStep.None),
        new("End", Phase.End, CombatStep.None),
    ];

    private bool IsStepActive(StepInfo step)
    {
        if (step.Phase == Phase.Combat)
            return CurrentPhase == Phase.Combat && CurrentCombatStep == step.CombatStep;
        return CurrentPhase == step.Phase;
    }

    private bool IsStepPast(StepInfo step)
    {
        if (IsStepActive(step)) return false;
        var activeIdx = Array.FindIndex(_steps, s => IsStepActive(s));
        var stepIdx = Array.IndexOf(_steps, step);
        return stepIdx < activeIdx;
    }

    private bool HasStop(StepInfo step)
    {
        if (StopSettings == null) return false;
        if (step.CombatStep != CombatStep.None)
            return StopSettings.ShouldStop(step.CombatStep);
        return StopSettings.ShouldStop(step.Phase);
    }

    private void ToggleStop(StepInfo step)
    {
        if (StopSettings == null) return;
        if (step.CombatStep != CombatStep.None)
            StopSettings.ToggleCombatStep(step.CombatStep);
        else
            StopSettings.TogglePhase(step.Phase);
    }
}
```

**Step 2: Create PhaseBar.razor.css**

```css
.phase-bar {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 4px 8px;
    background-color: rgba(0, 0, 0, 0.3);
    border-top: 1px solid rgba(255, 255, 255, 0.1);
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.phase-steps {
    display: flex;
    gap: 2px;
    flex: 1;
}

.phase-step {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 4px 8px;
    border-radius: 4px;
    cursor: pointer;
    user-select: none;
    min-width: 60px;
    transition: background-color 0.15s;
}

.phase-step:hover {
    background-color: rgba(255, 255, 255, 0.1);
}

.step-label {
    font-size: 0.7rem;
    font-weight: 500;
    opacity: 0.4;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.stop-dot {
    font-size: 0.5rem;
    opacity: 0.3;
    margin-top: 2px;
}

/* Active phase */
.phase-step.active {
    background-color: rgba(255, 193, 7, 0.25);
    border: 1px solid rgba(255, 193, 7, 0.5);
}

.phase-step.active .step-label {
    opacity: 1;
    color: #ffc107;
    font-weight: 700;
}

.phase-step.active .stop-dot {
    opacity: 0.8;
}

/* Past phases */
.phase-step.past .step-label {
    opacity: 0.6;
}

.phase-step.past .stop-dot {
    opacity: 0.5;
}

/* Phase with stop enabled */
.phase-step.has-stop .stop-dot {
    color: #66bb6a;
    opacity: 0.8;
}

.phase-actions {
    display: flex;
    align-items: center;
    gap: 4px;
    flex-shrink: 0;
}
```

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/PhaseBar.razor src/MtgDecker.Web/Components/Pages/Game/PhaseBar.razor.css
git commit -m "feat(web): add PhaseBar component with MTGO-style phase strip and stop toggles"
```

---

### Task 4: GameBoard Layout Restructure

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css`

**Step 1: Rewrite GameBoard.razor CSS to full-viewport grid**

Replace the current CSS in `GameBoard.razor.css`:

```css
.game-board {
    display: grid;
    grid-template-rows:
        auto          /* opponent info bar */
        1fr           /* opponent battlefield */
        auto          /* phase bar + stack */
        2fr           /* player battlefield */
        auto          /* player info bar */
        auto;         /* player hand */
    height: calc(100vh - 64px); /* subtract MudBlazor AppBar */
    padding: 4px;
    gap: 2px;
    position: relative;
}

.game-over-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.7);
    z-index: 9999;
}

/* Remove old two-column layout */
.board-main {
    display: contents; /* children participate in parent grid */
}
```

**Step 2: Rewrite GameBoard.razor markup**

Replace the markup structure. The key changes:
- Remove `.board-main` wrapper (use `display: contents`)
- Replace turn bar with `<PhaseBar>` component
- Move `<GameLogPanel>` into a slide-over overlay
- Split opponent and local player zones

The new structure should be:

```razor
<div class="game-board" tabindex="0" @ref="_boardRef" @onkeydown="HandleKeyDown">
    @if (IsGameOver) { /* game over overlay — keep as-is */ }

    @* Row 1: Opponent info bar *@
    <OpponentInfoBar ... />

    @* Row 2: Opponent battlefield *@
    <OpponentBattlefield ... />

    @* Row 3: Phase bar + stack *@
    <div class="phase-bar-row">
        <PhaseBar CurrentPhase="@State.CurrentPhase"
                  CurrentCombatStep="@State.CombatStep"
                  TurnNumber="@State.TurnNumber"
                  ActivePlayerName="@State.ActivePlayer.Name"
                  HasPriority="@HasPriority"
                  IsWaitingForCombat="@IsWaitingForCombat"
                  IsWaitingForAttackers="@(IsLocalActive && Handler?.IsWaitingForAttackers == true)"
                  IsWaitingForBlockers="@(IsLocalDefending && Handler?.IsWaitingForBlockers == true)"
                  CanUndo="@(LocalPlayer.ActionHistory.Count > 0)"
                  StackCount="@State.Stack.Count"
                  StopSettings="@_phaseStops"
                  OnPass="HandlePassOrConfirm"
                  OnUndo="() => OnUndo.InvokeAsync()"
                  OnSurrender="() => OnSurrender.InvokeAsync()" />
        <StackDisplay Stack="@State.Stack" />
    </div>

    @* Row 4: Player battlefield *@
    <PlayerBattlefield ... />

    @* Row 5: Player info bar *@
    <PlayerInfoBar ... />

    @* Row 6: Player hand *@
    <PlayerHand ... />

    @* Game log toggle + overlay *@
    <MudIconButton Class="log-toggle-btn" Icon="@Icons.Material.Filled.Article"
                   OnClick="ToggleLog" />
    @if (_showLog)
    {
        <div class="log-overlay" @onclick="ToggleLog">
            <div class="log-panel" @onclick:stopPropagation>
                <GameLogPanel GameLog="@State.GameLog" />
            </div>
        </div>
    }
</div>
```

Note: OpponentInfoBar, OpponentBattlefield, PlayerBattlefield, PlayerInfoBar, PlayerHand are not separate components yet — they're inline sections in GameBoard.razor that reference the new layout. Tasks 5-7 will create the actual sub-components.

For this task, keep the existing PlayerZone for both opponent and local player but place them in the new grid slots. The subsequent tasks will break them apart.

**Step 3: Add phase stops state and log toggle to @code**

Add to GameBoard.razor `@code` block:

```csharp
private PhaseStopSettings _phaseStops = new();
private bool _showLog = false;

private void ToggleLog() => _showLog = !_showLog;

private async Task HandlePassOrConfirm()
{
    if (Handler?.IsWaitingForAttackers == true && IsLocalActive)
    {
        // Confirm attackers — delegate to PlayerZone
        // This will be wired in Task 8
    }
    else if (Handler?.IsWaitingForBlockers == true && IsLocalDefending)
    {
        // Confirm blockers — delegate to PlayerZone
    }
    else
    {
        await PassPriority();
    }
}
```

**Step 4: Add log overlay CSS to GameBoard.razor.css**

```css
.log-toggle-btn {
    position: absolute;
    top: 8px;
    right: 8px;
    z-index: 100;
}

.log-overlay {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.3);
    z-index: 200;
    display: flex;
    justify-content: flex-end;
}

.log-panel {
    width: 350px;
    height: 100%;
    background-color: var(--mud-palette-surface);
    border-left: 1px solid rgba(255, 255, 255, 0.1);
    overflow-y: auto;
}

.phase-bar-row {
    display: flex;
    flex-direction: column;
    gap: 2px;
}
```

**Step 5: Verify app builds and runs**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`
Expected: Build succeeds (may have warnings)

**Step 6: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css
git commit -m "feat(web): restructure GameBoard to full-viewport grid with PhaseBar and log overlay"
```

---

### Task 5: Compact OpponentZone

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css`

Replace the opponent's `<PlayerZone IsOpponent="true">` with inline sections directly in GameBoard:

**Step 1: Replace opponent PlayerZone with compact inline sections**

In GameBoard.razor, replace the opponent PlayerZone call with:

```razor
@* Row 1: Opponent info bar *@
<div class="opponent-info-bar">
    <MudText Typo="Typo.subtitle2">@OpponentPlayer.Name</MudText>
    <MudChip T="string" Size="Size.Small" Color="Color.Error">♥ @OpponentPlayer.Life</MudChip>
    <MudChip T="string" Size="Size.Small" Color="Color.Default">Hand: @OpponentPlayer.Hand.Count</MudChip>
    <MudChip T="string" Size="Size.Small" Color="Color.Default">Grave: @OpponentPlayer.Graveyard.Count</MudChip>
    <MudChip T="string" Size="Size.Small" Color="Color.Default">Exile: @OpponentPlayer.Exile.Count</MudChip>
    <MudChip T="string" Size="Size.Small" Color="Color.Default">Lib: @OpponentPlayer.Library.Count</MudChip>
    @if (OpponentPlayer.ManaPool.Total > 0)
    {
        <div class="mana-pool-inline">
            @foreach (var (color, symbol) in ManaSymbols)
            {
                @if (OpponentPlayer.ManaPool[color] > 0)
                {
                    <span class="mana-entry">
                        <img src="https://svgs.scryfall.io/card-symbols/@(symbol).svg" alt="@color" class="mana-symbol-sm" />
                        <span>@OpponentPlayer.ManaPool[color]</span>
                    </span>
                }
            }
        </div>
    }
    @if (State.ActivePlayer == OpponentPlayer)
    {
        <MudChip T="string" Size="Size.Small" Color="Color.Primary">Active</MudChip>
    }
</div>

@* Row 2: Opponent battlefield (lands top, non-lands bottom) *@
<div class="opponent-battlefield">
    <div class="card-row opp-lands">
        @foreach (var card in OpponentPlayer.Battlefield.Cards.Where(c => c.IsLand))
        {
            <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl" Tapped="@card.IsTapped"
                         CardSize="90"
                         IsAttacking="@(State.Combat?.Attackers.Contains(card.Id) == true)"
                         IsBlocking="@IsCardBlocking(card.Id)"
                         Clickable="false" />
        }
    </div>
    <div class="card-row opp-creatures">
        @foreach (var card in OpponentPlayer.Battlefield.Cards.Where(c => !c.IsLand))
        {
            <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl" Tapped="@card.IsTapped"
                         Power="@card.Power" Toughness="@card.Toughness" DamageMarked="@card.DamageMarked"
                         CardSize="90"
                         IsAttacking="@(State.Combat?.Attackers.Contains(card.Id) == true)"
                         IsBlocking="@IsCardBlocking(card.Id)"
                         Clickable="@(Handler?.IsWaitingForTarget == true)"
                         OnClick="async () => await HandleOpponentCardClick(card)" />
        }
    </div>
</div>
```

**Step 2: Add CSS for opponent zones**

```css
.opponent-info-bar {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 4px 8px;
    background-color: rgba(255, 0, 0, 0.05);
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.opponent-battlefield {
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 4px;
    overflow: auto;
}

.card-row {
    display: flex;
    flex-wrap: wrap;
    gap: 2px;
    min-height: 40px;
}

.mana-pool-inline {
    display: flex;
    align-items: center;
    gap: 4px;
}

.mana-symbol-sm {
    width: 16px;
    height: 16px;
}

.mana-entry {
    display: flex;
    align-items: center;
    gap: 2px;
    font-size: 0.75rem;
}
```

**Step 3: Add helper methods to @code**

```csharp
private bool IsCardBlocking(Guid cardId) =>
    State.Combat?.Attackers.Any(a => State.Combat.GetBlockers(a).Contains(cardId)) == true;

private async Task HandleOpponentCardClick(GameCard card)
{
    if (Handler?.IsWaitingForTarget == true)
    {
        Handler.SubmitTarget(new TargetInfo(card.Id, OpponentPlayer.Id, ZoneType.Battlefield));
    }
}

private static readonly (ManaColor color, string symbol)[] ManaSymbols =
[
    (ManaColor.White, "W"), (ManaColor.Blue, "U"), (ManaColor.Black, "B"),
    (ManaColor.Red, "R"), (ManaColor.Green, "G"), (ManaColor.Colorless, "C")
];
```

**Step 4: Verify build**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css
git commit -m "feat(web): replace opponent PlayerZone with compact info bar + split battlefield"
```

---

### Task 6: CardDisplay Size Parameter

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor.css`

**Step 1: Add CardSize parameter**

In `CardDisplay.razor` `@code` block, add:

```csharp
[Parameter] public int CardSize { get; set; } = 100; // default stays backward-compatible
```

**Step 2: Use CardSize for dynamic sizing**

Replace the hardcoded `width: 100px` in the component. In the `<div class="card-display">` element, add an inline style:

```razor
<div class="card-display @(Tapped ? "tapped" : "") ..."
     style="width: @(CardSize)px; min-height: @(CardSize * 1.4)px;">
```

**Step 3: Update CardDisplay.razor.css**

Remove the hardcoded `width: 100px` and `min-height: 140px` from `.card-display`. Keep the rest.

```css
.card-display {
    display: inline-block;
    margin: 2px;
    transition: transform 0.15s ease;
    position: relative;
    border-radius: 6px;
    overflow: hidden;
}
```

**Step 4: Verify build**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor src/MtgDecker.Web/Components/Pages/Game/CardDisplay.razor.css
git commit -m "feat(web): add CardSize parameter to CardDisplay for 90px/130px sizing"
```

---

### Task 7: Player Battlefield with Land/Creature Separation

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css`

**Step 1: Replace local PlayerZone's battlefield with split rows**

In GameBoard.razor, replace the local PlayerZone with inline sections. Keep the PlayerZone for combat prompts, card choice, reveal, and targeting for now — but split the battlefield and hand into the grid rows.

Add below the phase bar row:

```razor
@* Row 4: Player battlefield (non-lands top, lands bottom) *@
<div class="player-battlefield">
    <div class="card-row player-creatures">
        @foreach (var card in LocalPlayer.Battlefield.Cards.Where(c => !c.IsLand))
        {
            var isEligibleAttacker = Handler?.IsWaitingForAttackers == true
                && Handler?.EligibleAttackers?.Any(c => c.Id == card.Id) == true;
            var isEligibleBlocker = Handler?.IsWaitingForBlockers == true
                && Handler?.EligibleBlockers?.Any(c => c.Id == card.Id) == true;
            <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl" Tapped="@card.IsTapped"
                         Power="@card.Power" Toughness="@card.Toughness" DamageMarked="@card.DamageMarked"
                         CardSize="130"
                         Selected="@(_selectedCard?.Id == card.Id)"
                         IsAttacking="@(_selectedAttackers.Contains(card.Id) || State.Combat?.Attackers.Contains(card.Id) == true)"
                         IsBlocking="@(_blockerAssignments.ContainsKey(card.Id) || IsCardBlocking(card.Id))"
                         Eligible="@(isEligibleAttacker || isEligibleBlocker)"
                         Clickable="true"
                         OnClick="async () => await HandleBattlefieldClick(card, isEligibleAttacker, isEligibleBlocker)" />
        }
    </div>
    <div class="card-row player-lands">
        @foreach (var card in LocalPlayer.Battlefield.Cards.Where(c => c.IsLand))
        {
            <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl" Tapped="@card.IsTapped"
                         CardSize="130"
                         Selected="@(_selectedCard?.Id == card.Id)"
                         Clickable="true"
                         OnClick="async () => await HandleLandClick(card)" />
        }
    </div>
</div>

@* Row 5: Player info bar *@
<div class="player-info-bar">
    <MudText Typo="Typo.subtitle2">@LocalPlayer.Name</MudText>
    <MudChip T="string" Size="Size.Small" Color="Color.Error">♥ @LocalPlayer.Life</MudChip>
    @if (LocalPlayer.ManaPool.Total > 0)
    {
        <div class="mana-pool-inline">
            @foreach (var (color, symbol) in ManaSymbols)
            {
                @if (LocalPlayer.ManaPool[color] > 0)
                {
                    <span class="mana-entry">
                        <img src="https://svgs.scryfall.io/card-symbols/@(symbol).svg" alt="@color" class="mana-symbol" />
                        <span>@LocalPlayer.ManaPool[color]</span>
                    </span>
                }
            }
        </div>
    }
    <MudChip T="string" Size="Size.Small" Color="Color.Default">Grave: @LocalPlayer.Graveyard.Count</MudChip>
    <MudChip T="string" Size="Size.Small" Color="Color.Default">Exile: @LocalPlayer.Exile.Count</MudChip>
    <MudChip T="string" Size="Size.Small" Color="Color.Default">Lib: @LocalPlayer.Library.Count</MudChip>
</div>

@* Row 6: Player hand *@
<div class="player-hand">
    @foreach (var card in LocalPlayer.Hand.Cards)
    {
        <CardDisplay Name="@card.Name" ImageUrl="@card.ImageUrl"
                     CardSize="130"
                     Selected="@(_selectedCard?.Id == card.Id)"
                     Clickable="true"
                     OnClick="async () => await HandleHandClick(card)" />
    }
</div>
```

**Step 2: Move combat/selection state from PlayerZone to GameBoard**

Move the following fields and methods from PlayerZone.razor `@code` to GameBoard.razor `@code`:
- `_selectedCard`, `_selectedZone`
- `_selectedAttackers`, `_blockerAssignments`, `_selectedBlocker`, `_blockerOrder`
- `HandleBattlefieldClick`, `ToggleAttacker`, `ConfirmAttackers`, `SkipAttack`
- `SelectBlocker`, `AssignBlockerToAttacker`, `ConfirmBlockers`, `SkipBlocking`
- `AddToBlockerOrder`, `ConfirmBlockerOrder`
- `HandlePlay`, `HandleTapToggle`
- `HandleLandClick` (new — tap for mana on click, or show color picker for choice lands)
- `HandleHandClick` (new — play land or cast spell on click)

**Step 3: Add CSS for player zones**

```css
.player-battlefield {
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 4px;
    overflow: auto;
}

.player-info-bar {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 4px 8px;
    border-top: 1px solid rgba(255, 255, 255, 0.05);
}

.player-hand {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: 2px;
    padding: 4px;
    background-color: rgba(0, 0, 0, 0.15);
    border-top: 1px solid rgba(255, 255, 255, 0.1);
    min-height: 60px;
}
```

**Step 4: Add Eligible parameter to CardDisplay**

In CardDisplay.razor, add:
```csharp
[Parameter] public bool Eligible { get; set; }
```

And in CardDisplay.razor.css:
```css
.card-display.eligible {
    box-shadow: 0 0 6px rgba(100, 200, 100, 0.6);
}
```

Apply the class conditionally in CardDisplay.razor:
```razor
<div class="card-display @(Eligible ? "eligible" : "") ...">
```

**Step 5: Verify build**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet build src/MtgDecker.Web/`

**Step 6: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/
git commit -m "feat(web): split player battlefield into land/creature rows with 130px cards"
```

---

### Task 8: Wire Combat to PhaseBar Buttons

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

**Step 1: Complete HandlePassOrConfirm**

Update the `HandlePassOrConfirm` method in GameBoard.razor to handle all three cases:

```csharp
private async Task HandlePassOrConfirm()
{
    if (Handler?.IsWaitingForAttackers == true && IsLocalActive)
    {
        await OnAttackersChosen.InvokeAsync(_selectedAttackers.ToList());
        _selectedAttackers.Clear();
    }
    else if (Handler?.IsWaitingForBlockers == true && IsLocalDefending)
    {
        await OnBlockersChosen.InvokeAsync(new Dictionary<Guid, Guid>(_blockerAssignments));
        _blockerAssignments.Clear();
        _selectedBlocker = null;
    }
    else
    {
        await PassPriority();
    }
}
```

**Step 2: Add Skip button to PhaseBar for combat**

In PhaseBar.razor, add a "Skip" button next to "Confirm Attacks" / "Confirm Blocks":

```razor
@if (HasPriority || IsWaitingForAttackers || IsWaitingForBlockers)
{
    @if (IsWaitingForAttackers || IsWaitingForBlockers)
    {
        <MudButton Size="Size.Small" Variant="Variant.Outlined" OnClick="OnSkipCombat">
            Skip
        </MudButton>
    }
    <MudButton Size="Size.Small" Variant="Variant.Filled" Color="Color.Primary"
               OnClick="OnPass">
        @PassButtonLabel
    </MudButton>
}
```

Add `[Parameter] public EventCallback OnSkipCombat { get; set; }` to PhaseBar.

Wire it in GameBoard:
```csharp
private async Task HandleSkipCombat()
{
    if (Handler?.IsWaitingForAttackers == true)
    {
        await OnAttackersChosen.InvokeAsync(Array.Empty<Guid>());
        _selectedAttackers.Clear();
    }
    else if (Handler?.IsWaitingForBlockers == true)
    {
        await OnBlockersChosen.InvokeAsync(new Dictionary<Guid, Guid>());
        _blockerAssignments.Clear();
    }
}
```

**Step 3: Verify build**

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/
git commit -m "feat(web): wire combat confirm/skip to PhaseBar buttons"
```

---

### Task 9: Prompts Migration (Card Choice, Reveal, Target, Mana Color, Blocker Order)

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`

The combat prompts moved to PhaseBar (Task 8). Now move the remaining prompts from PlayerZone into GameBoard. These are:
- Card choice prompt (tutor effects)
- Reveal cards prompt (Ringleader)
- Target picker
- Mana color picker
- Blocker ordering prompt

**Step 1: Move prompts to GameBoard.razor**

Place these prompts inline in the GameBoard, positioned as overlays on the player battlefield area. They show as floating panels above the battlefield when active.

The prompts themselves keep the same markup from PlayerZone — just move them into GameBoard and adjust positioning to appear centered over the player battlefield.

**Step 2: Add CSS for prompt overlays**

```css
.prompt-overlay {
    position: absolute;
    bottom: 200px; /* above hand */
    left: 50%;
    transform: translateX(-50%);
    z-index: 150;
    max-width: 600px;
    width: 90%;
}
```

**Step 3: Remove prompt parameters from PlayerZone**

After moving all prompts to GameBoard, the PlayerZone component is no longer needed (all its functionality has moved to GameBoard inline sections). Delete or keep as empty shell.

**Step 4: Verify build**

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/
git commit -m "feat(web): migrate all prompts from PlayerZone to GameBoard overlays"
```

---

### Task 10: Auto-Pass Integration

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor`

**Step 1: Wire auto-pass into the game loop**

In GamePage.razor, when the `InteractiveDecisionHandler` fires `OnWaitingForInput`, check if the handler should auto-pass for the current phase. If so, submit a Pass action automatically.

Find where `OnWaitingForInput` is handled and add:

```csharp
private void HandleWaitingForInput()
{
    InvokeAsync(async () =>
    {
        // Check auto-pass for phase stops
        if (_handler.IsWaitingForAction && _handler.ShouldAutoPass(
            _state.CurrentPhase, _state.CombatStep, _state.Stack.Count == 0))
        {
            var localPlayer = _playerSeat == 1 ? _state.Player1 : _state.Player2;
            if (_state.PriorityPlayer == localPlayer)
            {
                _handler.SubmitAction(GameAction.Pass(localPlayer.Id));
                return;
            }
        }

        StateHasChanged();
    });
}
```

**Step 2: Pass PhaseStopSettings from GamePage to GameBoard**

Ensure the `_phaseStops` object is shared so toggles in the PhaseBar persist.

**Step 3: Verify build and test manually**

Run the app, start a game. Verify:
- Phases without stops auto-pass (Untap, Upkeep, Draw skip instantly)
- Phases with stops pause for input (Main, Combat)
- Toggling a stop on the phase bar changes behavior next turn

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/GamePage.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor
git commit -m "feat(web): integrate auto-pass with PhaseStopSettings for faster gameplay"
```

---

### Task 11: Cleanup and Polish

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css`
- Possibly delete: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor` (if fully replaced)
- Possibly delete: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor.css`

**Step 1: Remove dead code**

- If PlayerZone is no longer referenced, delete it
- Remove any unused parameters, CSS classes, or methods from GameBoard
- Remove the old `.board-main` and two-column grid CSS

**Step 2: Visual polish**

- Ensure tapped cards display correctly at larger size
- Verify card images scale properly at 90px and 130px
- Check combat indicators (ATK/BLK badges) look good at new sizes
- Ensure mana symbols render at correct size in info bars
- Test game-over overlay still works

**Step 3: Run all tests**

Run: `export PATH="/c/Program Files/dotnet:$PATH" && dotnet test tests/MtgDecker.Engine.Tests/ -v minimal`
Expected: All tests pass (engine changes are backward-compatible)

**Step 4: Test manually in browser**

- Start a new game at http://localhost:5044/game/new
- Verify layout matches the design: opponent top, phase bar middle, you bottom
- Click phase bar steps to toggle stops
- Verify auto-pass works (phases without stops should fly by)
- Play a full game to verify all interactions work

**Step 5: Commit**

```bash
git add -A
git commit -m "chore(web): cleanup old PlayerZone, polish game UI layout"
```

---

### Task 12: Final Verification

**Step 1: Run full test suite**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ -v minimal
dotnet test tests/MtgDecker.Domain.Tests/ -v minimal
dotnet test tests/MtgDecker.Application.Tests/ -v minimal
dotnet test tests/MtgDecker.Infrastructure.Tests/ -v minimal
```

Expected: All tests pass

**Step 2: Visual smoke test**

Run the app and play through a full game with both combat and spell casting to verify:
- Phase bar progression
- Phase stop toggles
- Click-to-attack combat
- Game log slide-over
- Opponent zone is compact
- Land/creature rows separated
- Card sizes (90px opponent, 130px player)
- Stack display
- All prompts (tutor, reveal, targeting, mana color) work

**Step 3: Commit any final fixes**
