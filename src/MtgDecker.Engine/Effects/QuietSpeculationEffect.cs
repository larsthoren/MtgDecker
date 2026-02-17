namespace MtgDecker.Engine.Effects;

public class QuietSpeculationEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var controller = state.GetPlayer(spell.ControllerId);

        // Find cards with flashback in library
        var eligible = controller.Library.Cards
            .Where(c => CardDefinitions.TryGet(c.Name, out var def) && def.FlashbackCost != null)
            .ToList();

        int found = 0;
        while (found < 3 && eligible.Count > 0)
        {
            var chosenId = await handler.ChooseCard(
                eligible, $"Choose a flashback card ({found + 1}/3)", optional: found > 0, ct);

            if (!chosenId.HasValue) break;

            var card = controller.Library.RemoveById(chosenId.Value);
            if (card != null)
            {
                controller.Graveyard.Add(card);
                state.Log($"{controller.Name} puts {card.Name} into graveyard.");
                eligible.Remove(card);
                found++;
            }
        }

        controller.Library.Shuffle();
        if (found > 0)
            state.Log($"{controller.Name} searched for {found} flashback card(s).");
        else
            state.Log($"{controller.Name} finds no flashback cards.");
    }
}
