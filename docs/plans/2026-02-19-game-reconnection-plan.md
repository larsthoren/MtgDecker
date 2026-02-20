# Game Reconnection & Google Auth Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let players reconnect to active games after WebSocket disconnects, using Google OAuth for reliable player identity (invite-only).

**Architecture:** Add Google OAuth with cookie authentication and an invite-only allow-list. Track user IDs in game sessions so reconnecting players can rejoin their seat. Reuse the existing InteractiveDecisionHandler on reconnect (handler stays alive, TCS stays pending). Last-tab-wins for multi-tab.

**Tech Stack:** ASP.NET Core Authentication (Google + Cookies), EF Core migration, Blazor AuthenticationStateProvider, MudBlazor AuthorizeView.

---

### Task 1: Add Google Auth NuGet Package & User Entity Changes

**Files:**
- Modify: `src/MtgDecker.Web/MtgDecker.Web.csproj`
- Modify: `src/MtgDecker.Domain/Entities/User.cs`
- Modify: `src/MtgDecker.Infrastructure/Data/Configurations/UserConfiguration.cs`

**Step 1: Add NuGet package**

Run:
```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet add src/MtgDecker.Web/ package Microsoft.AspNetCore.Authentication.Google
```

**Step 2: Update User entity**

In `src/MtgDecker.Domain/Entities/User.cs`, replace contents:

```csharp
namespace MtgDecker.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string GoogleSubjectId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}
```

**Step 3: Update UserConfiguration**

In `src/MtgDecker.Infrastructure/Data/Configurations/UserConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.GoogleSubjectId).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.GoogleSubjectId).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
```

**Step 4: Create EF Core migration**

Run:
```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet ef migrations add AddUserAuthFields --project src/MtgDecker.Infrastructure/ --startup-project src/MtgDecker.Web/
```

**Step 5: Verify build**

Run:
```bash
dotnet build src/MtgDecker.Web/
```
Expected: Build succeeded.

**Step 6: Commit**

```bash
git add src/MtgDecker.Web/MtgDecker.Web.csproj src/MtgDecker.Domain/Entities/User.cs src/MtgDecker.Infrastructure/Data/Configurations/UserConfiguration.cs src/MtgDecker.Infrastructure/Data/Migrations/
git commit -m "feat: add Google auth NuGet package and User entity auth fields"
```

---

### Task 2: Configure Google OAuth Middleware in Program.cs

**Files:**
- Modify: `src/MtgDecker.Web/Program.cs`
- Modify: `src/MtgDecker.Web/appsettings.json`
- Modify: `src/MtgDecker.Web/appsettings.Development.json`

**Step 1: Add AllowedUsers and Google auth config to appsettings.json**

Add to the JSON root (keep existing keys):

```json
{
  "AllowedUsers": [
    "lars.thoren@gmail.com"
  ],
  "Authentication": {
    "Google": {
      "ClientId": "",
      "ClientSecret": ""
    }
  }
}
```

Note: Actual ClientId/ClientSecret should be stored in .NET User Secrets for dev. The appsettings.json just documents the config shape.

**Step 2: Store secrets using dotnet user-secrets**

Run:
```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet user-secrets init --project src/MtgDecker.Web/
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID" --project src/MtgDecker.Web/
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET" --project src/MtgDecker.Web/
```

Note: Replace with real values from Google Cloud Console. The engineer must create a Google OAuth 2.0 Client ID at https://console.cloud.google.com/apis/credentials with:
- Application type: Web application
- Authorized redirect URIs: `https://localhost:PORT/signin-google`

**Step 3: Add authentication services to Program.cs**

After `builder.Services.AddMudServices();` and before `builder.Services.AddApplication();`, add:

```csharp
// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
        ?? throw new InvalidOperationException("Google ClientId not configured.");
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
        ?? throw new InvalidOperationException("Google ClientSecret not configured.");
    options.Events.OnTicketReceived = context =>
    {
        var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
        var allowedUsers = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetSection("AllowedUsers").Get<string[]>() ?? [];

        if (email == null || !allowedUsers.Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/access-denied");
            context.HandleResponse();
        }
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllers();
```

Add these usings to the top of Program.cs:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
```

**Step 4: Add middleware to the pipeline**

After `app.UseHttpsRedirection();` and before `app.UseAntiforgery();`, add:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

After `app.MapStaticAssets();` and before `app.MapRazorComponents<App>()`, add:

```csharp
app.MapControllers();
```

**Step 5: Verify build**

Run:
```bash
dotnet build src/MtgDecker.Web/
```
Expected: Build succeeded.

**Step 6: Commit**

```bash
git add src/MtgDecker.Web/Program.cs src/MtgDecker.Web/appsettings.json
git commit -m "feat: configure Google OAuth middleware with invite-only access"
```

---

### Task 3: Create AccountController & AccessDenied Page

**Files:**
- Create: `src/MtgDecker.Web/Controllers/AccountController.cs`
- Create: `src/MtgDecker.Web/Components/Pages/AccessDenied.razor`

**Step 1: Create AccountController**

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

namespace MtgDecker.Web.Controllers;

[Route("account")]
public class AccountController : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = "/")
    {
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
}
```

**Step 2: Create AccessDenied page**

```razor
@page "/access-denied"

<PageTitle>Access Denied - MtgDecker</PageTitle>

<MudContainer MaxWidth="MaxWidth.Small" Class="mt-8 text-center">
    <MudIcon Icon="@Icons.Material.Filled.Lock" Size="Size.Large" Color="Color.Error" Class="mb-4" />
    <MudText Typo="Typo.h4" Class="mb-4">Access Restricted</MudText>
    <MudText Typo="Typo.body1" Class="mb-4">
        Your account is not on the invite list. Contact the administrator for access.
    </MudText>
    <MudButton Variant="Variant.Filled" Color="Color.Primary" Href="/">Back to Home</MudButton>
</MudContainer>
```

**Step 3: Verify build**

Run:
```bash
dotnet build src/MtgDecker.Web/
```
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Controllers/AccountController.cs src/MtgDecker.Web/Components/Pages/AccessDenied.razor
git commit -m "feat: add AccountController for login/logout and AccessDenied page"
```

---

### Task 4: Create UserService for Find-or-Create

**Files:**
- Create: `src/MtgDecker.Web/Services/UserService.cs`

**Step 1: Write the failing test**

There's no test project for the Web layer, and this is a simple service that wraps EF Core. We'll test it implicitly via integration. Skip TDD for this task.

**Step 2: Create UserService**

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MtgDecker.Domain.Entities;
using MtgDecker.Infrastructure.Data;

namespace MtgDecker.Web.Services;

public class UserService
{
    private readonly IServiceProvider _serviceProvider;

    public UserService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<User> GetOrCreateUserAsync(ClaimsPrincipal principal)
    {
        var googleSubjectId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No NameIdentifier claim found.");
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? "";
        var displayName = principal.FindFirstValue(ClaimTypes.Name) ?? email;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtgDeckerDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.DisplayName = displayName;
            await db.SaveChangesAsync();
            return user;
        }

        user = new User
        {
            DisplayName = displayName,
            Email = email,
            GoogleSubjectId = googleSubjectId,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<Guid> GetUserIdAsync(ClaimsPrincipal principal)
    {
        var user = await GetOrCreateUserAsync(principal);
        return user.Id;
    }
}
```

**Step 3: Register UserService in Program.cs**

After the authentication services block, add:

```csharp
builder.Services.AddScoped<MtgDecker.Web.Services.UserService>();
```

**Step 4: Verify build**

Run:
```bash
dotnet build src/MtgDecker.Web/
```
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Services/UserService.cs src/MtgDecker.Web/Program.cs
git commit -m "feat: add UserService for Google identity to User entity mapping"
```

---

### Task 5: Add Auth UI to MainLayout

**Files:**
- Modify: `src/MtgDecker.Web/Components/Layout/MainLayout.razor`
- Modify: `src/MtgDecker.Web/Components/_Imports.razor`

**Step 1: Add auth namespace to _Imports.razor**

Add to `src/MtgDecker.Web/Components/_Imports.razor`:

```razor
@using Microsoft.AspNetCore.Components.Authorization
```

**Step 2: Update MainLayout with login/logout**

In the `<MudAppBar>`, after the `<MudSpacer />` and before the dark mode toggle, add:

```razor
<AuthorizeView>
    <Authorized>
        <MudText Typo="Typo.body2" Class="mr-3">@context.User.Identity?.Name</MudText>
        <MudButton Variant="Variant.Text" Color="Color.Inherit" Href="/account/logout"
                   StartIcon="@Icons.Material.Filled.Logout">Logout</MudButton>
    </Authorized>
    <NotAuthorized>
        <MudButton Variant="Variant.Text" Color="Color.Inherit" Href="/account/login"
                   StartIcon="@Icons.Material.Filled.Login">Login</MudButton>
    </NotAuthorized>
</AuthorizeView>
```

**Step 3: Add CascadingAuthenticationState to Routes**

In `src/MtgDecker.Web/Components/App.razor`, the `<Routes>` component needs auth state. Since we added `AddCascadingAuthenticationState()` in Program.cs, Blazor .NET 10 automatically wraps the component tree — no changes needed here.

**Step 4: Verify build**

Run:
```bash
dotnet build src/MtgDecker.Web/
```
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Layout/MainLayout.razor src/MtgDecker.Web/Components/_Imports.razor
git commit -m "feat: add login/logout buttons and user display to navbar"
```

---

### Task 6: Replace Hardcoded UserId Across All Pages

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/MyDecks.razor` (line 81)
- Modify: `src/MtgDecker.Web/Components/Pages/DeckBuilder.razor` (line 247)
- Modify: `src/MtgDecker.Web/Components/Pages/MyCollection.razor` (line 84)
- Modify: `src/MtgDecker.Web/Components/Pages/AddCollectionDialog.razor` (line 36)
- Modify: `src/MtgDecker.Web/Components/Pages/CardDetailDialog.razor` (line 190)
- Modify: `src/MtgDecker.Web/Components/Pages/CreateDeckDialog.razor` (line 31)
- Modify: `src/MtgDecker.Web/Components/Pages/ImportData.razor` (line 105)
- Modify: `src/MtgDecker.Web/Components/Pages/GameLobby.razor` (line 138)

**Pattern for each page/dialog:**

Replace the hardcoded UserId pattern. For pages, inject `UserService` and `AuthenticationStateProvider`:

```razor
@inject MtgDecker.Web.Services.UserService UserService
@inject AuthenticationStateProvider AuthStateProvider
```

Remove the line:
```csharp
private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
```

Add a field and resolve it in `OnInitializedAsync`:
```csharp
private Guid _userId;

protected override async Task OnInitializedAsync()
{
    var authState = await AuthStateProvider.GetAuthenticationStateAsync();
    _userId = await UserService.GetUserIdAsync(authState.User);
    // ... rest of existing init logic, using _userId instead of UserId ...
}
```

For dialogs (AddCollectionDialog, CardDetailDialog, CreateDeckDialog), pass UserId as a `[Parameter]` from the parent page instead of hardcoding it:

```csharp
[Parameter] public Guid UserId { get; set; }
```

The parent pages already have the authenticated userId; pass it when opening dialogs via `DialogParameters`.

**Step 1: Update each file**

Apply the pattern above to all 8 files. Change all references from `UserId` to `_userId` (pages) or the parameter (dialogs).

**Step 2: Update Program.cs seed data**

The seed data in Program.cs still uses the hardcoded Guid. This is fine — it seeds sample decks for a known test user. No change needed here since seed data runs before any auth flow.

**Step 3: Verify build**

Run:
```bash
dotnet build src/MtgDecker.Web/
```
Expected: Build succeeded.

**Step 4: Run existing tests**

Run:
```bash
dotnet test tests/MtgDecker.Domain.Tests/
dotnet test tests/MtgDecker.Application.Tests/
dotnet test tests/MtgDecker.Infrastructure.Tests/
```
Expected: All pass. These tests don't depend on the web layer.

**Step 5: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/
git commit -m "feat: replace hardcoded UserId with authenticated user across all pages"
```

---

### Task 7: Add UserId & ConnectionId to GameSession

**Files:**
- Modify: `src/MtgDecker.Engine/GameSession.cs`

**Step 1: Write the failing test**

In `tests/MtgDecker.Engine.Tests/GameSessionTests.cs` (new file):

```csharp
using FluentAssertions;
using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests;

public class GameSessionTests
{
    [Fact]
    public void GetSeatForUser_ReturnsCorrectSeat()
    {
        var session = new GameSession("TEST01");
        var deck1 = new List<GameCard> { GameCard.Create("Mountain", "Basic Land — Mountain", null) };
        var deck2 = new List<GameCard> { GameCard.Create("Forest", "Basic Land — Forest", null) };

        session.JoinPlayer("Alice", "user-alice", deck1);
        session.JoinPlayer("Bob", "user-bob", deck2);

        session.GetSeatForUser("user-alice").Should().Be(1);
        session.GetSeatForUser("user-bob").Should().Be(2);
        session.GetSeatForUser("user-unknown").Should().BeNull();
    }

    [Fact]
    public void ReconnectPlayer_ReturnsExistingHandler_AndUpdatesConnectionId()
    {
        var session = new GameSession("TEST02");
        var deck1 = CreateMinimalDeck();
        var deck2 = CreateMinimalDeck();

        session.JoinPlayer("Alice", "user-alice", deck1);
        session.JoinPlayer("Bob", "user-bob", deck2);

        session.SetConnectionId(1, "conn-old");

        var handler = session.ReconnectPlayer(1, "conn-new");

        session.GetActiveConnectionId(1).Should().Be("conn-new");
        // Handler is null before StartAsync — that's expected
        // After StartAsync, ReconnectPlayer returns the live handler
    }

    [Fact]
    public void GetActiveConnectionId_ReturnsNull_WhenNoConnectionSet()
    {
        var session = new GameSession("TEST03");
        session.GetActiveConnectionId(1).Should().BeNull();
        session.GetActiveConnectionId(2).Should().BeNull();
    }

    private static List<GameCard> CreateMinimalDeck()
    {
        var cards = new List<GameCard>();
        for (int i = 0; i < 7; i++)
            cards.Add(GameCard.Create("Mountain", "Basic Land — Mountain", null));
        return cards;
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameSessionTests"
```
Expected: FAIL — `JoinPlayer` doesn't accept userId parameter, `GetSeatForUser` doesn't exist, etc.

**Step 3: Implement GameSession changes**

In `src/MtgDecker.Engine/GameSession.cs`:

Add properties:
```csharp
public string? Player1UserId { get; private set; }
public string? Player2UserId { get; private set; }
private string? _player1ConnectionId;
private string? _player2ConnectionId;
```

Modify `JoinPlayer` to accept `userId`:
```csharp
public int JoinPlayer(string playerName, string userId, List<GameCard> deck)
{
    lock (_joinLock)
    {
        if (Player1Name == null)
        {
            Player1Name = playerName;
            Player1UserId = userId;
            _player1Deck = deck;
            return 1;
        }
        if (Player2Name == null)
        {
            Player2Name = playerName;
            Player2UserId = userId;
            _player2Deck = deck;
            return 2;
        }
        throw new InvalidOperationException("Game is full.");
    }
}
```

Also keep the old overload for AI bot joining (no userId):
```csharp
public int JoinPlayer(string playerName, List<GameCard> deck)
    => JoinPlayer(playerName, "", deck);
```

Add new methods:
```csharp
public int? GetSeatForUser(string userId)
{
    if (string.IsNullOrEmpty(userId)) return null;
    if (Player1UserId == userId) return 1;
    if (Player2UserId == userId) return 2;
    return null;
}

public void SetConnectionId(int seat, string connectionId)
{
    if (seat == 1) _player1ConnectionId = connectionId;
    else _player2ConnectionId = connectionId;
}

public string? GetActiveConnectionId(int seat)
    => seat == 1 ? _player1ConnectionId : _player2ConnectionId;

public InteractiveDecisionHandler? ReconnectPlayer(int seat, string connectionId)
{
    lock (_stateLock)
    {
        LastActivity = DateTime.UtcNow;
        SetConnectionId(seat, connectionId);
        OnStateChanged?.Invoke();
        return GetHandler(seat);
    }
}
```

**Step 4: Run test to verify it passes**

Run:
```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameSessionTests"
```
Expected: PASS.

**Step 5: Run all engine tests to ensure no regressions**

Run:
```bash
dotnet test tests/MtgDecker.Engine.Tests/
```
Expected: All pass. The old `JoinPlayer(name, deck)` overload keeps existing callers working.

**Step 6: Commit**

```bash
git add src/MtgDecker.Engine/GameSession.cs tests/MtgDecker.Engine.Tests/GameSessionTests.cs
git commit -m "feat: add UserId, ConnectionId, and ReconnectPlayer to GameSession"
```

---

### Task 8: Add Active Game Tracking to GameSessionManager

**Files:**
- Modify: `src/MtgDecker.Engine/GameSessionManager.cs`

**Step 1: Write the failing test**

In `tests/MtgDecker.Engine.Tests/GameSessionManagerTests.cs` (new file):

```csharp
using FluentAssertions;
using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests;

public class GameSessionManagerTests
{
    [Fact]
    public void SetActiveGame_And_GetActiveGameId_RoundTrips()
    {
        var manager = new GameSessionManager();
        manager.SetActiveGame("user-1", "GAME01");

        manager.GetActiveGameId("user-1").Should().Be("GAME01");
    }

    [Fact]
    public void GetActiveGameId_ReturnsNull_WhenNoActiveGame()
    {
        var manager = new GameSessionManager();
        manager.GetActiveGameId("user-1").Should().BeNull();
    }

    [Fact]
    public void ClearActiveGame_RemovesMapping()
    {
        var manager = new GameSessionManager();
        manager.SetActiveGame("user-1", "GAME01");
        manager.ClearActiveGame("user-1");

        manager.GetActiveGameId("user-1").Should().BeNull();
    }

    [Fact]
    public void RemoveSession_ClearsActiveGamesForPlayers()
    {
        var manager = new GameSessionManager();
        var session = manager.CreateGame();
        var deck = new List<GameCard>();
        for (int i = 0; i < 7; i++)
            deck.Add(GameCard.Create("Mountain", "Basic Land — Mountain", null));

        session.JoinPlayer("Alice", "user-alice", deck);
        manager.SetActiveGame("user-alice", session.GameId);

        manager.RemoveSession(session.GameId);

        manager.GetActiveGameId("user-alice").Should().BeNull();
    }
}
```

**Step 2: Run test to verify it fails**

Run:
```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameSessionManagerTests"
```
Expected: FAIL — methods don't exist.

**Step 3: Implement GameSessionManager changes**

In `src/MtgDecker.Engine/GameSessionManager.cs`:

```csharp
using System.Collections.Concurrent;

namespace MtgDecker.Engine;

public class GameSessionManager
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _activeGames = new(); // userId -> gameId

    public GameSession CreateGame()
    {
        var gameId = GenerateGameId();
        var session = new GameSession(gameId);
        _sessions[gameId] = session;
        return session;
    }

    public GameSession? GetSession(string gameId) =>
        _sessions.TryGetValue(gameId, out var session) ? session : null;

    public void RemoveSession(string gameId)
    {
        if (_sessions.TryRemove(gameId, out var session))
        {
            // Clear active game mappings for players in this session
            if (!string.IsNullOrEmpty(session.Player1UserId))
                ClearActiveGame(session.Player1UserId);
            if (!string.IsNullOrEmpty(session.Player2UserId))
                ClearActiveGame(session.Player2UserId);
            session.Dispose();
        }
    }

    public void SetActiveGame(string userId, string gameId)
    {
        if (!string.IsNullOrEmpty(userId))
            _activeGames[userId] = gameId;
    }

    public void ClearActiveGame(string userId)
        => _activeGames.TryRemove(userId, out _);

    public string? GetActiveGameId(string userId)
        => _activeGames.TryGetValue(userId, out var id) ? id : null;

    public IEnumerable<string> GetStaleSessionIds(TimeSpan maxInactivity)
    {
        var cutoff = DateTime.UtcNow - maxInactivity;
        return _sessions
            .Where(kvp => kvp.Value.LastActivity < cutoff || kvp.Value.IsGameOver)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private string GenerateGameId()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string id;
        do
        {
            id = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
        } while (_sessions.ContainsKey(id));
        return id;
    }
}
```

**Step 4: Run test to verify it passes**

Run:
```bash
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameSessionManagerTests"
```
Expected: PASS.

**Step 5: Commit**

```bash
git add src/MtgDecker.Engine/GameSessionManager.cs tests/MtgDecker.Engine.Tests/GameSessionManagerTests.cs
git commit -m "feat: add active game tracking to GameSessionManager"
```

---

### Task 9: Update GameLobby with Auth & Active Game Banner

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/GameLobby.razor`

**Step 1: Update GameLobby**

Key changes:
1. Inject `UserService` and `AuthenticationStateProvider`
2. Replace hardcoded `UserId` with authenticated user
3. Pass `userId` string to `JoinPlayer`
4. Track active game in `GameSessionManager` after creating/joining
5. Add active game banner at top of page

Add injections:
```razor
@inject MtgDecker.Web.Services.UserService UserService
@inject AuthenticationStateProvider AuthStateProvider
```

Add active game banner before the Create/Join grid:
```razor
@if (_activeGameId != null)
{
    <MudAlert Severity="Severity.Info" Class="mb-4" Dense="true">
        <MudText>You have an active game in progress.</MudText>
        <MudButton Variant="Variant.Filled" Color="Color.Secondary" Size="Size.Small"
                   Class="ml-2" Href="@($"/game/{_activeGameId}?seat={_activeSeat}")">
            Rejoin Game
        </MudButton>
    </MudAlert>
}
```

In `OnInitializedAsync`:
```csharp
var authState = await AuthStateProvider.GetAuthenticationStateAsync();
_userId = await UserService.GetUserIdAsync(authState.User);
_userIdString = authState.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "";

// Check for active game
_activeGameId = SessionManager.GetActiveGameId(_userIdString);
if (_activeGameId != null)
{
    var activeSession = SessionManager.GetSession(_activeGameId);
    _activeSeat = activeSession?.GetSeatForUser(_userIdString);
}
```

In `CreateGame` and `JoinGame`, after joining:
```csharp
var seat = session.JoinPlayer(_playerName, _userIdString, deck);
SessionManager.SetActiveGame(_userIdString, session.GameId);
```

Add fields:
```csharp
private Guid _userId;
private string _userIdString = "";
private string? _activeGameId;
private int? _activeSeat;
```

**Step 2: Verify build**

Run:
```bash
dotnet build src/MtgDecker.Web/
```
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/GameLobby.razor
git commit -m "feat: add active game banner and auth-based player tracking to GameLobby"
```

---

### Task 10: Update GamePage with Reconnection Logic

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor`

**Step 1: Update GamePage**

Key changes:
1. Inject `AuthenticationStateProvider` (not UserService — avoid DB call on every state change)
2. Generate a `connectionId` on init
3. On init, check if user already has a seat (reconnecting vs new)
4. If reconnecting, call `ReconnectPlayer` and reuse the handler
5. Add stale tab detection in `HandleStateChanged`
6. Add keepalive timer
7. On dispose, don't remove session if game is still in progress (other player may still be connected)

Add injections:
```razor
@inject AuthenticationStateProvider AuthStateProvider
```

Add fields:
```csharp
private string _connectionId = Guid.NewGuid().ToString();
private string _userIdString = "";
private bool _isStaleTab;
private System.Threading.Timer? _keepaliveTimer;
```

Replace `OnInitializedAsync`:
```csharp
protected override async Task OnInitializedAsync()
{
    _session = SessionManager.GetSession(GameId);
    if (_session == null) return;

    var authState = await AuthStateProvider.GetAuthenticationStateAsync();
    _userIdString = authState.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "";

    // Check if we're reconnecting to an existing seat
    var existingSeat = _session.GetSeatForUser(_userIdString);
    if (existingSeat.HasValue)
    {
        // Reconnecting — reuse existing handler
        _playerSeat = existingSeat.Value;
        var handler = _session.ReconnectPlayer(_playerSeat, _connectionId);
        if (handler != null)
        {
            handler.OnWaitingForInput += HandleWaitingForInput;
        }
    }
    else
    {
        // New join via query parameter (legacy flow)
        _playerSeat = SeatParam ?? 1;
        _session.SetConnectionId(_playerSeat, _connectionId);
    }

    _session.OnStateChanged += HandleStateChanged;

    if (!_session.IsStarted)
    {
        _gameStarting = true;
        await _session.StartAsync();

        // Subscribe AFTER StartAsync — handlers are created inside StartAsync
        var handler = _session.GetHandler(_playerSeat);
        if (handler != null)
        {
            handler.OnWaitingForInput += HandleWaitingForInput;
        }
    }

    // Start keepalive timer (30s interval)
    _keepaliveTimer = new System.Threading.Timer(_ =>
    {
        _session?.UpdateActivity();
    }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
}
```

Add `using System.Security.Claims;` to the `@code` block or `_Imports.razor`.

Add stale tab check in `HandleStateChanged`:
```csharp
private void HandleStateChanged()
{
    // Check if another tab has taken over our seat
    if (_session != null && _session.GetActiveConnectionId(_playerSeat) != _connectionId)
    {
        _isStaleTab = true;
    }
    InvokeAsync(StateHasChanged);
}
```

Add stale tab overlay at the top of the markup:
```razor
@if (_isStaleTab)
{
    <MudOverlay Visible="true" DarkBackground="true" ZIndex="9999">
        <MudPaper Class="pa-8 text-center" Elevation="4">
            <MudIcon Icon="@Icons.Material.Filled.TabUnselected" Size="Size.Large" Color="Color.Warning" Class="mb-4" />
            <MudText Typo="Typo.h5" Class="mb-2">Reconnected in Another Tab</MudText>
            <MudText Typo="Typo.body1" Class="mb-4">This game session is active in another browser tab.</MudText>
            <MudButton Variant="Variant.Filled" Color="Color.Primary" Href="/game/new">Back to Lobby</MudButton>
        </MudPaper>
    </MudOverlay>
}
```

Add `UpdateActivity` method to `GameSession.cs`:
```csharp
public void UpdateActivity()
{
    LastActivity = DateTime.UtcNow;
}
```

Update `Dispose`:
```csharp
public void Dispose()
{
    _keepaliveTimer?.Dispose();
    if (_session != null)
    {
        _session.OnStateChanged -= HandleStateChanged;
        var handler = _session.GetHandler(_playerSeat);
        if (handler != null)
        {
            handler.OnWaitingForInput -= HandleWaitingForInput;
        }
        // Only clean up if game is over AND we're the active connection
        if (_session.IsGameOver && _session.GetActiveConnectionId(_playerSeat) == _connectionId)
        {
            SessionManager.RemoveSession(_session.GameId);
        }
    }
}
```

**Step 2: Add System.Security.Claims to _Imports.razor**

Add to `src/MtgDecker.Web/Components/_Imports.razor`:
```razor
@using System.Security.Claims
```

**Step 3: Verify build**

Run:
```bash
dotnet build src/MtgDecker.Web/
```
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/GamePage.razor src/MtgDecker.Engine/GameSession.cs src/MtgDecker.Web/Components/_Imports.razor
git commit -m "feat: add game reconnection, stale tab detection, and keepalive to GamePage"
```

---

### Task 11: Update Cleanup Service Timeout

**Files:**
- Modify: `src/MtgDecker.Web/Services/GameSessionCleanupService.cs`

**Step 1: Extend cleanup timeout**

Change `MaxInactivity` from 30 minutes to 2 hours:

```csharp
private static readonly TimeSpan MaxInactivity = TimeSpan.FromHours(2);
```

**Step 2: Commit**

```bash
git add src/MtgDecker.Web/Services/GameSessionCleanupService.cs
git commit -m "feat: extend game session cleanup timeout to 2 hours"
```

---

### Task 12: Run Full Test Suite & Manual Verification

**Step 1: Run all tests**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Domain.Tests/
dotnet test tests/MtgDecker.Application.Tests/
dotnet test tests/MtgDecker.Infrastructure.Tests/
dotnet test tests/MtgDecker.Engine.Tests/
```
Expected: All pass.

**Step 2: Build and run the app**

```bash
dotnet run --project src/MtgDecker.Web/
```

Verify in browser:
1. App loads, shows Login button in navbar
2. Clicking Login redirects to Google sign-in
3. Non-invited email shows AccessDenied page
4. Invited email logs in, shows display name + Logout button
5. Creating a game works with authenticated user
6. Opening `/game/{id}` in a second tab shows "Reconnected in Another Tab" overlay on the first tab
7. Closing a tab and reopening the game URL reconnects seamlessly

**Step 3: Commit any fixes**

If manual testing reveals issues, fix them and commit.

---

### Task 13: Final Cleanup & PR

**Step 1: Review all changes**

Verify no hardcoded UserId references remain (except Program.cs seed data, which is intentional).

**Step 2: Create PR**

```bash
git push -u origin feat/game-reconnection
gh pr create --title "feat: game reconnection with Google OAuth" --body "$(cat <<'EOF'
## Summary
- Add Google OAuth authentication (invite-only, configured via appsettings AllowedUsers)
- Players can reconnect to active games after WebSocket disconnects
- Handler reuse: game loop stays alive, player picks up exactly where they left off
- Auto-redirect: lobby shows banner when player has an active game
- Last-tab-wins: opening game in second tab disables the first
- Keepalive: 30s heartbeat prevents premature session cleanup
- Cleanup timeout extended from 30min to 2h

## Test plan
- [ ] Verify Google login flow with invited email
- [ ] Verify non-invited email gets AccessDenied page
- [ ] Verify game creation/joining works with authenticated user
- [ ] Verify reconnection: close tab, reopen game URL, game resumes
- [ ] Verify stale tab: open game in two tabs, first shows overlay
- [ ] Verify active game banner in lobby
- [ ] All 1539+ existing tests pass

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
