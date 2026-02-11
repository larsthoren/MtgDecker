namespace MtgDecker.Engine;

public abstract class SpellEffect
{
    public abstract void Resolve(GameState state, StackObject spell);
}
