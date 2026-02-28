using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Mana;

public class ManaPool
{
    private readonly Dictionary<ManaColor, int> _pool = new();

    public int this[ManaColor color] => _pool.GetValueOrDefault(color, 0);
    public int Total
    {
        get
        {
            var sum = 0;
            foreach (var kv in _pool)
                sum += kv.Value;
            return sum;
        }
    }

    public IReadOnlyDictionary<ManaColor, int> Available =>
        _pool.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

    public void Add(ManaColor color, int amount = 1)
    {
        if (amount <= 0) return;
        _pool[color] = _pool.GetValueOrDefault(color, 0) + amount;
    }

    public bool CanPay(ManaCost cost)
    {
        var coloredTotal = 0;
        foreach (var (color, required) in cost.ColorRequirements)
        {
            if (this[color] < required) return false;
            coloredTotal += required;
        }
        return Total - coloredTotal >= cost.GenericCost;
    }

    public bool Pay(ManaCost cost)
    {
        if (!CanPay(cost)) return false;
        foreach (var (color, required) in cost.ColorRequirements)
        {
            _pool[color] -= required;
            if (_pool[color] == 0) _pool.Remove(color);
        }
        var remaining = cost.GenericCost;
        while (remaining > 0)
        {
            // Find color with largest pool (no sort allocation)
            ManaColor largestColor = default;
            int largestValue = 0;
            foreach (var (color, value) in _pool)
            {
                if (value > largestValue)
                {
                    largestColor = color;
                    largestValue = value;
                }
            }
            if (largestValue <= 0) break;
            var take = Math.Min(remaining, largestValue);
            _pool[largestColor] -= take;
            if (_pool[largestColor] == 0) _pool.Remove(largestColor);
            remaining -= take;
        }
        return true;
    }

    public bool CanPayWithPhyrexian(ManaCost cost, int playerLife)
    {
        // If no Phyrexian cost, delegate to regular CanPay
        if (!cost.HasPhyrexianCost)
            return CanPay(cost);

        // Check color requirements first
        foreach (var (color, required) in cost.ColorRequirements)
            if (this[color] < required) return false;

        // Calculate remaining mana after color requirements
        var remainingPool = new Dictionary<ManaColor, int>();
        foreach (var kvp in Available)
        {
            var after = kvp.Value;
            if (cost.ColorRequirements.TryGetValue(kvp.Key, out var needed))
                after -= needed;
            if (after > 0)
                remainingPool[kvp.Key] = after;
        }

        // For Phyrexian requirements: use colored mana where available, life for the rest
        int lifeNeeded = 0;
        foreach (var (color, required) in cost.PhyrexianRequirements)
        {
            var availableMana = remainingPool.GetValueOrDefault(color, 0);
            var paidWithMana = Math.Min(availableMana, required);
            var paidWithLife = required - paidWithMana;
            lifeNeeded += paidWithLife * 2;
            if (paidWithMana > 0)
            {
                remainingPool[color] = availableMana - paidWithMana;
                if (remainingPool[color] == 0) remainingPool.Remove(color);
            }
        }

        if (playerLife < lifeNeeded) return false;

        // Check generic cost against remaining pool
        var totalRemaining = remainingPool.Values.Sum();
        return totalRemaining >= cost.GenericCost;
    }

    public void Deduct(ManaColor color, int amount)
    {
        if (!_pool.ContainsKey(color)) return;
        _pool[color] = Math.Max(0, _pool[color] - amount);
        if (_pool[color] == 0) _pool.Remove(color);
    }

    public void Clear() => _pool.Clear();
}
