# In-Game Chat Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add chat between two players in a game, sharing the existing log panel with tab switching.

**Architecture:** ChatMessage record + list on GameSession (ephemeral). GameBoard gains tab bar in log panel. No engine changes.

**Tech Stack:** Blazor, MudBlazor, C#

---

### Task 1: Add ChatMessage record and GameSession chat support

**Files:**
- Modify: `src/MtgDecker.Engine/GameSession.cs`

**Step 1: Write the failing test**

Create `tests/MtgDecker.Engine.Tests/GameSessionChatTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Engine;

namespace MtgDecker.Engine.Tests;

public class GameSessionChatTests
{
    [Fact]
    public void AddChatMessage_StoresMessage()
    {
        var session = new GameSession("test-1");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(1, "Hello!");

        session.ChatMessages.Should().HaveCount(1);
        session.ChatMessages[0].PlayerName.Should().Be("Alice");
        session.ChatMessages[0].Text.Should().Be("Hello!");
        session.ChatMessages[0].PlayerSeat.Should().Be(1);
    }

    [Fact]
    public void AddChatMessage_Player2_UsesCorrectName()
    {
        var session = new GameSession("test-2");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(2, "Hi there");

        session.ChatMessages.Should().HaveCount(1);
        session.ChatMessages[0].PlayerName.Should().Be("Bob");
    }

    [Fact]
    public void AddChatMessage_EmptyText_IsIgnored()
    {
        var session = new GameSession("test-3");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(1, "");
        session.AddChatMessage(1, "   ");

        session.ChatMessages.Should().BeEmpty();
    }

    [Fact]
    public void AddChatMessage_TruncatesAt200Chars()
    {
        var session = new GameSession("test-4");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(1, new string('x', 300));

        session.ChatMessages[0].Text.Should().HaveLength(200);
    }

    [Fact]
    public void ChatMessages_ReturnsSnapshot()
    {
        var session = new GameSession("test-5");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        session.AddChatMessage(1, "First");
        var snapshot = session.ChatMessages;
        session.AddChatMessage(2, "Second");

        snapshot.Should().HaveCount(1, "snapshot should not reflect later additions");
        session.ChatMessages.Should().HaveCount(2);
    }

    [Fact]
    public void AddChatMessage_FiresOnStateChanged()
    {
        var session = new GameSession("test-6");
        session.JoinPlayer("Alice", new List<GameCard>());
        session.JoinPlayer("Bob", new List<GameCard>());

        bool fired = false;
        session.OnStateChanged += () => fired = true;

        session.AddChatMessage(1, "Hello!");

        fired.Should().BeTrue();
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameSessionChatTests"`
Expected: FAIL — `AddChatMessage` and `ChatMessages` don't exist.

**Step 3: Write minimal implementation**

Add to `GameSession.cs`:

```csharp
public record ChatMessage(string PlayerName, int PlayerSeat, string Text, DateTime Timestamp);
```

Add fields and methods inside `GameSession`:

```csharp
private readonly List<ChatMessage> _chatMessages = new();

public IReadOnlyList<ChatMessage> ChatMessages
{
    get { lock (_stateLock) return _chatMessages.ToList(); }
}

public void AddChatMessage(int playerSeat, string text)
{
    if (string.IsNullOrWhiteSpace(text)) return;
    if (text.Length > 200) text = text[..200];
    var name = playerSeat == 1 ? Player1Name : Player2Name;
    if (name == null) return;

    lock (_stateLock)
    {
        _chatMessages.Add(new ChatMessage(name, playerSeat, text, DateTime.UtcNow));
    }
    OnStateChanged?.Invoke();
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Engine.Tests/ --filter "GameSessionChatTests"`
Expected: PASS (all 6 tests)

**Step 5: Commit**

```bash
git add tests/MtgDecker.Engine.Tests/GameSessionChatTests.cs src/MtgDecker.Engine/GameSession.cs
git commit -m "feat(engine): add ChatMessage and chat support to GameSession"
```

---

### Task 2: Add tab bar to log panel and chat UI in GameBoard

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css`

**Step 1: Add new parameters and state to GameBoard**

In the `@code` block, add:

```csharp
[Parameter] public IReadOnlyList<ChatMessage>? ChatMessages { get; set; }
[Parameter] public EventCallback<string> OnChatSent { get; set; }
[Parameter] public string? LocalPlayerName { get; set; }

private string _activeTab = "log"; // "log" or "chat"
private string _chatInput = "";
```

Add `@using MtgDecker.Engine` is already present (for ChatMessage record which lives in the Engine namespace).

**Step 2: Replace the log panel section**

Replace the log toggle button and log overlay (lines ~738-749) with:

```razor
@* Log/Chat toggle button *@
<MudButton StartIcon="@Icons.Material.Filled.Article" Size="Size.Small"
           Variant="Variant.Outlined" Class="log-toggle-btn" OnClick="ToggleLog" Title="Toggle Panel">
    @(_activeTab == "chat" ? "Chat" : "Log")
</MudButton>

@* Log/Chat overlay *@
@if (_showLog)
{
    <div class="log-overlay" @onclick="ToggleLog">
        <div class="log-panel" @onclick:stopPropagation>
            <div class="panel-tabs">
                <MudButton Size="Size.Small" Variant="@(_activeTab == "log" ? Variant.Filled : Variant.Text)"
                           Color="Color.Primary" OnClick="@(() => _activeTab = "log")">Log</MudButton>
                <MudButton Size="Size.Small" Variant="@(_activeTab == "chat" ? Variant.Filled : Variant.Text)"
                           Color="Color.Primary" OnClick="@(() => _activeTab = "chat")">Chat</MudButton>
            </div>

            @if (_activeTab == "log")
            {
                <GameLogPanel GameLog="@State.GameLog" />
            }
            else
            {
                <div class="chat-panel">
                    <div class="chat-messages" @ref="_chatContainer">
                        @if (ChatMessages != null)
                        {
                            @foreach (var msg in ChatMessages)
                            {
                                var isLocal = msg.PlayerName == LocalPlayerName;
                                <div class="chat-message @(isLocal ? "chat-local" : "chat-remote")">
                                    <span class="chat-sender">@msg.PlayerName</span>
                                    <span class="chat-text">@msg.Text</span>
                                </div>
                            }
                        }
                    </div>
                    <div class="chat-input-row">
                        <MudTextField @bind-Value="_chatInput" Placeholder="Type a message..."
                                      Variant="Variant.Outlined" Margin="Margin.Dense"
                                      Immediate="true" Class="chat-input"
                                      OnKeyDown="HandleChatKeyDown" />
                        <MudIconButton Icon="@Icons.Material.Filled.Send" Size="Size.Small"
                                       Color="Color.Primary" OnClick="SendChat" />
                    </div>
                </div>
            }
        </div>
    </div>
}
```

**Step 3: Add chat methods to @code block**

```csharp
private ElementReference _chatContainer;

private async Task HandleChatKeyDown(KeyboardEventArgs e)
{
    if (e.Key == "Enter")
        await SendChat();
}

private async Task SendChat()
{
    if (string.IsNullOrWhiteSpace(_chatInput)) return;
    await OnChatSent.InvokeAsync(_chatInput.Trim());
    _chatInput = "";
}
```

**Step 4: Add auto-scroll for chat**

In `OnAfterRenderAsync`, add after the existing `FocusAsync`:

```csharp
if (_activeTab == "chat" && ChatMessages?.Count > 0)
{
    await JS.InvokeVoidAsync("scrollToBottom", _chatContainer);
}
```

Note: Need to inject `IJSRuntime` — check if already available. GameLogPanel injects it but GameBoard may not. Add `[Inject] private IJSRuntime JS { get; set; } = default!;` if needed.

**Step 5: Add CSS styles**

Append to `GameBoard.razor.css`:

```css
/* Panel tabs */
.panel-tabs {
    display: flex;
    gap: 4px;
    padding: 8px 8px 4px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

/* Chat panel */
.chat-panel {
    display: flex;
    flex-direction: column;
    height: calc(100% - 45px);
}

.chat-messages {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
}

.chat-message {
    margin-bottom: 6px;
    font-size: 0.85rem;
}

.chat-sender {
    font-weight: 600;
    margin-right: 6px;
}

.chat-local .chat-sender {
    color: #64b5f6;
}

.chat-remote .chat-sender {
    color: #ef9a9a;
}

.chat-text {
    word-break: break-word;
}

.chat-input-row {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 4px 8px 8px;
    border-top: 1px solid rgba(255, 255, 255, 0.1);
}

.chat-input {
    flex: 1;
}
```

**Step 6: Build to verify**

Run: `dotnet build src/MtgDecker.Web/`
Expected: 0 errors

**Step 7: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor.css
git commit -m "feat(web): add chat tab to log panel with message display and input"
```

---

### Task 3: Wire chat in GamePage

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor`

**Step 1: Add ChatMessages and OnChatSent to GameBoard invocation**

Add to the `<GameBoard>` tag:

```razor
ChatMessages="@_session.ChatMessages"
OnChatSent="HandleChatSent"
LocalPlayerName="@(_playerSeat == 1 ? _session.Player1Name : _session.Player2Name)"
```

**Step 2: Add HandleChatSent method**

```csharp
private void HandleChatSent(string text)
{
    _session?.AddChatMessage(_playerSeat, text);
}
```

**Step 3: Build to verify**

Run: `dotnet build src/MtgDecker.Web/`
Expected: 0 errors

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/GamePage.razor
git commit -m "feat(web): wire chat messages between GamePage and GameBoard"
```

---

### Task 4: Run all tests and verify

**Step 1: Run all engine tests**

Run: `dotnet test tests/MtgDecker.Engine.Tests/`
Expected: All pass (1724 + 6 new = 1730)

**Step 2: Build the full web project**

Run: `dotnet build src/MtgDecker.Web/`
Expected: 0 errors

**Step 3: Commit (if any cleanup needed)**

Only commit if adjustments were required.
