using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class GameState
{
    public Player Player1 { get; }
    public Player Player2 { get; }
    public Player ActivePlayer { get; set; }
    public Player PriorityPlayer { get; set; }
    public Phase CurrentPhase { get; set; }
    public int TurnNumber { get; set; }
    public bool IsGameOver { get; set; }
    public string? Winner { get; set; }
    public bool IsFirstTurn { get; set; }
    public CombatStep CombatStep { get; set; } = CombatStep.None;
    public CombatState? Combat { get; set; }
    private readonly List<IStackObject> _stack = new();
    private readonly object _stackLock = new();

    /// <summary>
    /// Thread-safe snapshot of the stack for UI rendering.
    /// Engine code should use StackPush/StackPopTop/StackPeekTop for mutations.
    /// </summary>
    public List<IStackObject> Stack
    {
        get { lock (_stackLock) { return _stack.ToList(); } }
    }

    public int StackCount { get { lock (_stackLock) { return _stack.Count; } } }

    public void StackPush(IStackObject item) { lock (_stackLock) { _stack.Add(item); } }

    public bool StackRemove(IStackObject item) { lock (_stackLock) { return _stack.Remove(item); } }

    public IStackObject? StackPeekTop()
    {
        lock (_stackLock) { return _stack.Count > 0 ? _stack[^1] : null; }
    }

    public IStackObject? StackPopTop()
    {
        lock (_stackLock)
        {
            if (_stack.Count == 0) return null;
            var top = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            return top;
        }
    }

    public Queue<Guid> ExtraTurns { get; } = new();
    public List<ContinuousEffect> ActiveEffects { get; } = new();
    public List<DelayedTrigger> DelayedTriggers { get; } = new();
    public long NextEffectTimestamp { get; set; } = 1;
    public event Action? OnStateChanged;

    private readonly object _logLock = new();
    private readonly List<string> _gameLog = new();

    /// <summary>
    /// Thread-safe snapshot of the game log. Returns a copy to avoid
    /// collection-modified-during-enumeration exceptions when the UI
    /// iterates while the game loop appends.
    /// </summary>
    public List<string> GameLog
    {
        get
        {
            lock (_logLock)
            {
                return _gameLog.ToList();
            }
        }
    }

    public GameState(Player player1, Player player2)
    {
        Player1 = player1;
        Player2 = player2;
        ActivePlayer = player1;
        PriorityPlayer = player1;
        CurrentPhase = Phase.Untap;
        TurnNumber = 1;
    }

    public Player GetOpponent(Player player) =>
        player == Player1 ? Player2 : Player1;

    public Player GetPlayer(Guid playerId) =>
        playerId == Player1.Id ? Player1 : Player2;

    public void Log(string message)
    {
        lock (_logLock)
        {
            _gameLog.Add(message);
        }
        OnStateChanged?.Invoke();
    }

    // Mid-cast state for MTGO-style manual payment
    public GameCard? PendingCastCard { get; private set; }
    public Guid? PendingCastPlayerId { get; private set; }
    public int RemainingGenericCost { get; private set; }
    public Dictionary<ManaColor, int> RemainingPhyrexianCost { get; private set; } = new();
    public bool IsMidCast => PendingCastCard != null;
    public bool IsFullyPaid => IsMidCast && RemainingGenericCost == 0 && TotalRemainingPhyrexian == 0;
    public int TotalRemainingPhyrexian => RemainingPhyrexianCost.Values.Sum();

    // Refunded mana tracking for cancel
    internal Dictionary<ManaColor, int> MidCastAutoDeducted { get; set; } = new();
    internal int MidCastLifePaid { get; set; }

    public void BeginMidCast(Guid playerId, GameCard card, int genericCost, Dictionary<ManaColor, int> phyrexianCost)
    {
        PendingCastPlayerId = playerId;
        PendingCastCard = card;
        RemainingGenericCost = genericCost;
        RemainingPhyrexianCost = new Dictionary<ManaColor, int>(phyrexianCost);
        MidCastAutoDeducted = new Dictionary<ManaColor, int>();
        MidCastLifePaid = 0;
    }

    public void ClearMidCast()
    {
        PendingCastCard = null;
        PendingCastPlayerId = null;
        RemainingGenericCost = 0;
        RemainingPhyrexianCost.Clear();
        MidCastAutoDeducted.Clear();
        MidCastLifePaid = 0;
    }

    public void ApplyManaPayment(ManaColor color)
    {
        if (RemainingPhyrexianCost.TryGetValue(color, out var phyCount) && phyCount > 0)
        {
            RemainingPhyrexianCost[color] = phyCount - 1;
            if (RemainingPhyrexianCost[color] == 0) RemainingPhyrexianCost.Remove(color);
        }
        else if (RemainingGenericCost > 0)
        {
            RemainingGenericCost--;
        }
    }

    public bool ApplyLifePayment()
    {
        var first = RemainingPhyrexianCost.FirstOrDefault(kv => kv.Value > 0);
        if (first.Value == 0 && !RemainingPhyrexianCost.ContainsKey(first.Key)) return false;

        RemainingPhyrexianCost[first.Key] = first.Value - 1;
        if (RemainingPhyrexianCost[first.Key] == 0) RemainingPhyrexianCost.Remove(first.Key);
        MidCastLifePaid += 2;
        return true;
    }
}
