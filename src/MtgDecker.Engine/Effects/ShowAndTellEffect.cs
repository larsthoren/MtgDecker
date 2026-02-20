using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class ShowAndTellEffect : SpellEffect
{
    private static bool IsPermanent(GameCard card) =>
        card.CardTypes.HasFlag(CardType.Creature) ||
        card.CardTypes.HasFlag(CardType.Artifact) ||
        card.CardTypes.HasFlag(CardType.Enchantment) ||
        card.CardTypes.HasFlag(CardType.Land);

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        var opponent = state.GetOpponent(caster);

        // Caster chooses first
        GameCard? casterCard = null;
        var casterPermanents = caster.Hand.Cards.Where(IsPermanent).ToList();
        if (casterPermanents.Count > 0)
        {
            var casterChoice = await caster.DecisionHandler.ChooseCard(
                casterPermanents, "Choose a permanent card to put onto the battlefield", optional: true, ct);
            if (casterChoice.HasValue)
                casterCard = caster.Hand.Cards.FirstOrDefault(c => c.Id == casterChoice.Value);
        }

        // Opponent chooses
        GameCard? opponentCard = null;
        var opponentPermanents = opponent.Hand.Cards.Where(IsPermanent).ToList();
        if (opponentPermanents.Count > 0)
        {
            var opponentChoice = await opponent.DecisionHandler.ChooseCard(
                opponentPermanents, "Choose a permanent card to put onto the battlefield", optional: true, ct);
            if (opponentChoice.HasValue)
                opponentCard = opponent.Hand.Cards.FirstOrDefault(c => c.Id == opponentChoice.Value);
        }

        // Put both onto the battlefield simultaneously
        if (casterCard != null)
        {
            caster.Hand.RemoveById(casterCard.Id);
            caster.Battlefield.Add(casterCard);
            casterCard.TurnEnteredBattlefield = state.TurnNumber;
            if (casterCard.EntersTapped) casterCard.IsTapped = true;
            state.Log($"{caster.Name} puts {casterCard.Name} onto the battlefield.");
        }
        else
        {
            state.Log($"{caster.Name} chooses not to put a card onto the battlefield.");
        }

        if (opponentCard != null)
        {
            opponent.Hand.RemoveById(opponentCard.Id);
            opponent.Battlefield.Add(opponentCard);
            opponentCard.TurnEnteredBattlefield = state.TurnNumber;
            if (opponentCard.EntersTapped) opponentCard.IsTapped = true;
            state.Log($"{opponent.Name} puts {opponentCard.Name} onto the battlefield.");
        }
        else
        {
            state.Log($"{opponent.Name} chooses not to put a card onto the battlefield.");
        }
    }
}
