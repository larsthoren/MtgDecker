namespace MtgDecker.Engine.Effects;

public class DrawCardsEffect : SpellEffect
{
    public int Count { get; }

    public DrawCardsEffect(int count) => Count = count;

    public override void Resolve(GameState state, StackObject spell)
    {
        var player = state.GetPlayer(spell.ControllerId);
        var drawn = 0;
        for (int i = 0; i < Count; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card == null) break;
            player.Hand.Add(card);
            drawn++;
        }
        state.Log($"{player.Name} draws {drawn} card(s).");
    }
}
