using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Effects;

public class DestroyAllByColorEffect : SpellEffect
{
    public ManaColor Color { get; }
    public CardType? CardTypeFilter { get; }

    public DestroyAllByColorEffect(ManaColor color, CardType? cardTypeFilter = null)
    {
        Color = color;
        CardTypeFilter = cardTypeFilter;
    }

    private bool HasColor(ManaCost? cost) =>
        cost != null && cost.ColorRequirements.ContainsKey(Color);

    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            var targets = player.Battlefield.Cards
                .Where(c => HasColor(c.ManaCost))
                .Where(c => CardTypeFilter == null || c.CardTypes.HasFlag(CardTypeFilter.Value))
                .ToList();

            foreach (var card in targets)
            {
                player.Battlefield.Remove(card);
                player.Graveyard.Add(card);
            }
        }

        var description = CardTypeFilter.HasValue
            ? $"{spell.Card.Name} destroys all {Color.ToString().ToLower()} {CardTypeFilter.Value.ToString().ToLower()}s."
            : $"{spell.Card.Name} destroys all {Color.ToString().ToLower()} permanents.";
        state.Log(description);
    }
}
