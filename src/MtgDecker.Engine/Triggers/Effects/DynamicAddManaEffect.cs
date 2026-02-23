using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class DynamicAddManaEffect : IEffect
{
    public ManaColor Color { get; }
    public Func<Player, int>? CountFunc { get; }
    public Func<GameState, int>? StateCountFunc { get; }

    /// <summary>
    /// Dynamic mana based on controller's state only.
    /// </summary>
    public DynamicAddManaEffect(ManaColor color, Func<Player, int> countFunc)
    {
        Color = color;
        CountFunc = countFunc;
    }

    /// <summary>
    /// Dynamic mana based on full game state (both players).
    /// Used for effects like Priest of Titania that count all Elves on the battlefield.
    /// </summary>
    public DynamicAddManaEffect(ManaColor color, Func<GameState, int> stateCountFunc)
    {
        Color = color;
        StateCountFunc = stateCountFunc;
    }

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var amount = StateCountFunc != null
            ? StateCountFunc(context.State)
            : CountFunc!(context.Controller);

        if (amount > 0)
        {
            context.Controller.ManaPool.Add(Color, amount);
            context.State.Log($"{context.Controller.Name} adds {amount} {Color} mana from {context.Source.Name}.");
        }
        else
        {
            context.State.Log($"{context.Source.Name} produces no mana.");
        }
        return Task.CompletedTask;
    }
}
