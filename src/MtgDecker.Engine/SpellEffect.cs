namespace MtgDecker.Engine;

public abstract class SpellEffect
{
    /// <summary>
    /// Synchronous resolution for simple effects. Override this for effects
    /// that don't need player interaction during resolution.
    /// </summary>
    public virtual void Resolve(GameState state, StackObject spell) { }

    /// <summary>
    /// Async resolution that provides a decision handler for interactive effects
    /// (e.g., Brainstorm, Ponder). By default delegates to the sync Resolve() method.
    /// Override this for effects that need to prompt the player during resolution.
    /// </summary>
    public virtual Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        Resolve(state, spell);
        return Task.CompletedTask;
    }
}
