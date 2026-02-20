# Game Reconnection & Google Auth Design

## Problem

Game sessions are entirely in-memory. When a player's Blazor Server circuit drops (WebSocket 1006 disconnect), they lose their connection to the game with no way to rejoin. The game loop continues running on the server, but the player's UI is gone. This happens regularly during extended play (~20 min).

## Goals

1. Let players reconnect to an active game after a WebSocket disconnect
2. Add Google OAuth authentication for reliable player identity
3. Auto-redirect returning players to their active game
4. Handle multiple browser tabs gracefully (last tab wins)

## Non-Goals (Future Phases)

- Server restart persistence (serializing game state to DB)
- Spectator mode
- Match history / game recording
- Multi-server scaling (sticky sessions / distributed state)
- Open registration (invite-only for now)

---

## 1. Google OAuth Authentication

### Setup

- Add `Microsoft.AspNetCore.Authentication.Google` NuGet package
- Configure Google OAuth in `Program.cs` with `AddAuthentication().AddGoogle()`
- Store client ID/secret via .NET User Secrets (dev) / environment variables (prod)
- Use Blazor's `AuthenticationStateProvider` to access the logged-in user

### Invite-Only Access

- **No open registration.** Only pre-approved email addresses can log in.
- Maintain an allow-list of invited emails in `appsettings.json` (or a DB table for dynamic management)
- On Google login callback, check the user's email against the allow-list
- If email is not in the allow-list, reject login with a friendly "Access restricted" page
- If email is allowed: look up existing User by Google subject ID, or create a new User entity on first login
- Replace the hardcoded `UserId` constant with the real authenticated user's ID throughout the web layer

```json
// appsettings.json
{
  "AllowedUsers": [
    "lars.thoren@gmail.com",
    "friend@example.com"
  ]
}
```

### UI Changes

- Add a Login/Logout button to the nav bar
- Protect game and deck pages with `[Authorize]` -- redirect to login if unauthenticated
- Display the user's Google display name in the UI

### User Entity Changes

```csharp
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

---

## 2. Session Tracking & Player Identity

### GameSessionManager Changes

Add a `ConcurrentDictionary<string, string>` mapping `userId -> gameId` for active game lookup:

```csharp
public class GameSessionManager
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _activeGames = new(); // userId -> gameId

    public void SetActiveGame(string userId, string gameId)
        => _activeGames[userId] = gameId;

    public void ClearActiveGame(string userId)
        => _activeGames.TryRemove(userId, out _);

    public string? GetActiveGameId(string userId)
        => _activeGames.TryGetValue(userId, out var id) ? id : null;
}
```

### GameSession Changes

Store `UserId` per seat and track active connection IDs:

```csharp
public class GameSession
{
    // Existing properties...
    public string? Player1UserId { get; private set; }
    public string? Player2UserId { get; private set; }
    private string? _player1ConnectionId;
    private string? _player2ConnectionId;

    public int JoinPlayer(string playerName, string userId, List<GameCard> deck)
    {
        // Store userId alongside playerName
    }

    public InteractiveDecisionHandler? ReconnectPlayer(int seat, string connectionId)
    {
        // Update the connection ID for the seat
        // Return the existing (still-alive) InteractiveDecisionHandler
        // Fire OnStateChanged so the new circuit renders current state
    }

    public string? GetActiveConnectionId(int seat)
        => seat == 1 ? _player1ConnectionId : _player2ConnectionId;

    public int? GetSeatForUser(string userId)
    {
        if (Player1UserId == userId) return 1;
        if (Player2UserId == userId) return 2;
        return null;
    }
}
```

### Connection Tracking

- Each `GamePage` component generates a unique connection ID (GUID) on init
- On `Dispose` (circuit death), the connection ID becomes stale but the handler stays alive
- On reconnect, the new circuit calls `ReconnectPlayer` with its new connection ID
- If the old circuit is still alive (multi-tab case), it detects its connection ID is stale and shows "Reconnected elsewhere"

---

## 3. UI Reconnection Flow

### Auto-Redirect on Page Load

In `GameLobby.razor` `OnInitializedAsync`:

1. Get the authenticated user ID from `AuthenticationStateProvider`
2. Call `GameSessionManager.GetActiveGameId(userId)`
3. If an active game exists, show a banner: "You have an active game in progress" with a **Rejoin** button
4. Clicking Rejoin navigates to `/game/{activeGameId}`

### GamePage Reconnection

`GamePage.razor` `OnInitializedAsync` flow:

1. Get authenticated user ID from `AuthenticationStateProvider`
2. Get session from `GameSessionManager`
3. Check if user is already in a seat: `session.GetSeatForUser(userId)`
4. **If yes (reconnecting):**
   - Call `session.ReconnectPlayer(seat, myConnectionId)`
   - Get the existing `InteractiveDecisionHandler` (still alive, still waiting for input)
   - Subscribe to `OnStateChanged`
   - Render the current game state -- player picks up exactly where they left off
5. **If no (new join):**
   - Normal join flow as today

### Stale Tab Detection

- Each component stores its `connectionId`
- On `OnStateChanged` callback, check if `session.GetActiveConnectionId(mySeat) == myConnectionId`
- If not, display an overlay: "You've reconnected in another tab"

### Keepalive

- Add a periodic timer (every 30s) from the Blazor circuit calling `session.UpdateActivity()`
- This prevents the cleanup service from removing games where the player is still connected but idle
- Extend cleanup timeout from 30 min to 2h (configurable) for games with disconnected players

---

## 4. Handler Reuse on Reconnect

The `InteractiveDecisionHandler` uses a `TaskCompletionSource` pattern. When a player disconnects:

- The game loop is blocked in an `await` on the handler's TCS
- The handler is still alive (referenced by the `GameSession`)
- The TCS is still pending (no result set)

When the player reconnects:

- The new Blazor component gets the same handler instance
- The UI renders the current game state (what action is being requested)
- The player submits their decision, which completes the existing TCS
- The game loop resumes from exactly where it was blocked

No cancellation, no re-prompting. The handler is a stable intermediary between the game loop and whatever UI circuit happens to be connected.

---

## 5. Error Handling

- **Game ended during disconnect:** If cleanup ran or the game ended naturally, show "This game has ended" with a link back to the lobby
- **Handler TCS faulted:** If the TCS was somehow completed/faulted during disconnect (edge case), log the error and let the game loop handle it (it already catches exceptions)
- **Opponent surrendered:** Normal game-over flow; reconnecting player sees the result
- **AI opponent:** AI games should also be reconnectable -- the AI handler runs independently of any circuit

---

## 6. Testing Strategy

### Unit Tests

- `GameSessionManager` -- active game tracking (`SetActiveGame`, `GetActiveGameId`, `ClearActiveGame`)
- `GameSession.ReconnectPlayer` -- verifies handler reuse, connection ID swap
- `GameSession.GetSeatForUser` -- user ID to seat mapping
- Stale tab detection -- connection ID comparison
- User mapping -- Google subject ID to User entity creation/lookup

### Integration Tests

- Simulate circuit death: verify handler stays alive, TCS remains pending
- Simulate reconnection: verify new connection picks up state
- Simulate multi-tab: verify last connection wins, old tab gets notified
- Auth flow: Google login creates/finds User entity

---

## 7. Files to Create/Modify

| File | Change |
|------|--------|
| `Program.cs` | Add Google auth middleware, cookie auth, AuthenticationStateProvider |
| `User.cs` | Add GoogleSubjectId, Email, CreatedAt, LastLoginAt properties |
| `MtgDeckerDbContext.cs` | Update User entity configuration |
| New migration | Add columns to Users table |
| `GameSessionManager.cs` | Add userId->gameId mapping, GetActiveGameId |
| `GameSession.cs` | Add UserId per seat, ConnectionId, ReconnectPlayer, GetSeatForUser |
| `GameLobby.razor` | Active game banner, auth-gated join |
| `GamePage.razor` | Reconnection logic, stale tab detection, keepalive timer |
| `NavMenu.razor` / `MainLayout.razor` | Login/logout button, user display name |
| New: `AccountController.cs` | Google OAuth login/logout/callback endpoints |
| New: `UserService.cs` | Google ID to User entity mapping (find-or-create, invite check) |
| New: `AccessDenied.razor` | "Access restricted" page for non-invited emails |
| New: migration file | EF Core migration for User entity changes |

---

## 8. Implementation Order

1. **Google OAuth setup** -- NuGet, Program.cs config, User entity changes, migration
2. **UserService** -- find-or-create user from Google identity
3. **Auth UI** -- login/logout in nav, `[Authorize]` on pages, replace hardcoded UserId
4. **GameSession changes** -- UserId per seat, ConnectionId, ReconnectPlayer
5. **GameSessionManager changes** -- active game tracking
6. **GamePage reconnection** -- reconnect flow, stale tab detection
7. **GameLobby auto-redirect** -- active game banner
8. **Keepalive** -- periodic ping, extended cleanup timeout
9. **Tests** -- unit + integration for all new behavior
