using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

/// <summary>
/// A planeswalker loyalty ability. LoyaltyCost is positive for +N, negative for -N, zero for 0.
/// </summary>
public record LoyaltyAbility(int LoyaltyCost, IEffect Effect, string Description);
