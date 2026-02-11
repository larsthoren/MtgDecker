namespace MtgDecker.Engine.Triggers;

public interface IEffect
{
    Task Execute(EffectContext context, CancellationToken ct = default);
}
