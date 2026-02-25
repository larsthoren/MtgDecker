# Game UI Layout Improvements Design

## Goal

Improve the game mode UI by auto-collapsing the nav drawer for maximum board space and making both player battlefields equal size.

## Changes

### 1. Nav Drawer Auto-Collapse in Game Mode

**File:** `src/MtgDecker.Web/Components/Layout/MainLayout.razor`

**Current behavior:** Nav drawer starts open. User manually toggles via hamburger button.

**New behavior:**
- Subscribe to `NavigationManager.LocationChanged`
- When URL matches `/game/` pattern, auto-collapse the drawer
- Store the previous `_drawerOpen` state before collapsing
- When navigating away from a game page, restore the previous state
- Hamburger menu button remains available for manual override during gameplay
- Implement `IDisposable` to unsubscribe from the event

**Logic:**
```csharp
private bool _drawerOpen = true;
private bool _drawerStateBeforeGame = true;
private bool _isGamePage = false;

protected override void OnInitialized()
{
    NavigationManager.LocationChanged += OnLocationChanged;
    CheckGamePage(NavigationManager.Uri);
}

private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
{
    CheckGamePage(e.Location);
    InvokeAsync(StateHasChanged);
}

private void CheckGamePage(string uri)
{
    var wasGamePage = _isGamePage;
    _isGamePage = uri.Contains("/game/", StringComparison.OrdinalIgnoreCase);

    if (_isGamePage && !wasGamePage)
    {
        _drawerStateBeforeGame = _drawerOpen;
        _drawerOpen = false;
    }
    else if (!_isGamePage && wasGamePage)
    {
        _drawerOpen = _drawerStateBeforeGame;
    }
}

public void Dispose()
{
    NavigationManager.LocationChanged -= OnLocationChanged;
}
```

### 2. Equal Board Layout

**File:** `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css`

**Current grid:**
```css
grid-template-rows: auto 1fr auto 2fr auto auto;
```
- Row 2 (opponent battlefield): `1fr`
- Row 4 (player battlefield): `2fr` (double height)

**New grid:**
```css
grid-template-rows: auto 1fr auto 1fr auto auto;
```
- Row 2 (opponent battlefield): `1fr`
- Row 4 (player battlefield): `1fr` (equal height)

Both battlefields now share remaining vertical space equally after the auto-sized rows (info bars, phase bar, hand).

## What Stays the Same

- Card size: Fixed 146px width
- Hand: Bottom row with horizontal scroll
- Stack display: Floating fixed-position on right side
- AppBar: 64px height, always visible
- Board height: `calc(100vh - 64px)`
- All card states (tapped, selected, attacking, etc.)
- Log/chat overlay toggle

## Testing

- Navigate to game page → drawer should auto-collapse
- Navigate away → drawer should restore to previous state
- Manually toggle drawer during game → should work normally
- Return to game after manual toggle → should auto-collapse again
- Both battlefields should appear equal height visually
