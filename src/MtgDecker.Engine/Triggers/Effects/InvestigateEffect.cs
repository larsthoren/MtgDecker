using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Creates a Clue artifact token with an activated ability:
/// Pay {2}, sacrifice this artifact: Draw a card.
/// </summary>
public class InvestigateEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var clue = new GameCard
        {
            Name = "Clue",
            CardTypes = CardType.Artifact,
            IsToken = true,
            TurnEnteredBattlefield = context.State.TurnNumber,
            TokenActivatedAbility = new ActivatedAbility(
                Cost: new ActivatedAbilityCost(
                    SacrificeSelf: true,
                    ManaCost: ManaCost.Parse("{2}")
                ),
                Effect: new SacrificeAndDrawEffect()
            ),
        };

        context.Controller.Battlefield.Add(clue);
        context.State.Log($"{context.Controller.Name} investigates and creates a Clue token.");

        return Task.CompletedTask;
    }
}
