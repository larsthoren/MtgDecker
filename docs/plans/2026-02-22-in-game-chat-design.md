# In-Game Chat Design

## Goal

Add chat between two players in a game, sharing the existing log panel. Players toggle between game log and chat using tabs within the same side panel.

## Architecture

Chat is purely a session/UI concern. No engine changes needed.

### Data Model

`ChatMessage` record on `GameSession`:

```csharp
public record ChatMessage(string PlayerName, int PlayerSeat, string Text, DateTime Timestamp);
```

`GameSession` additions:
- `List<ChatMessage>` — ephemeral, lost on disconnect
- `AddChatMessage(int playerSeat, string text)` — adds message, fires `OnStateChanged`
- `ChatMessages` property — returns snapshot (`ToList()`) for thread safety
- Uses existing `_stateLock` for thread safety

### UI

The log panel gains a tab bar at the top: **Log | Chat**. A `_logTab` field (`Log` or `Chat`) controls which content is shown. No new component files — chat UI lives inline in `GameBoard.razor`.

**Chat tab:**
- Messages scroll vertically, newest at bottom
- Each message: `PlayerName: text`, local player color-coded
- `MudTextField` at bottom with Enter-to-send
- Max 200 chars per message, empty messages ignored
- Reuses existing `scrollToBottom` JS interop

**Toggle button** stays in the corner. Panel defaults to last-active tab.

### Data Flow

1. Player types message, hits Enter
2. `GameBoard` fires `OnChatSent` EventCallback
3. `GamePage` calls `session.AddChatMessage(playerSeat, text)`
4. `GameSession` adds to list, invokes `OnStateChanged`
5. Both players re-render

### Parameters

`GameBoard` new parameters:
- `ChatMessages` (IReadOnlyList<ChatMessage>)
- `OnChatSent` (EventCallback<string>)
- `LocalPlayerName` (string)

### Testing

- `GameSession.AddChatMessage` — verify message added, list readable
- No engine tests needed
