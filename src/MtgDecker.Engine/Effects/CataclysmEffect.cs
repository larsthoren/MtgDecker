using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

/// <summary>
/// Cataclysm -- Each player chooses one artifact, one creature, one enchantment,
/// and one land they control, then sacrifices the rest.
/// </summary>
public class CataclysmEffect : SpellEffect
{
    private static readonly CardType[] TypeOrder =
    [
        CardType.Artifact,
        CardType.Creature,
        CardType.Enchantment,
        CardType.Land
    ];

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        var opponent = state.GetOpponent(caster);

        // Active player (caster) chooses first, then opponent
        var casterKeepers = await ChooseKeepersForPlayer(caster, state, ct);
        var opponentKeepers = await ChooseKeepersForPlayer(opponent, state, ct);

        // Sacrifice everything not chosen
        SacrificeNonKeepers(caster, casterKeepers, state, spell);
        SacrificeNonKeepers(opponent, opponentKeepers, state, spell);
    }

    private static async Task<HashSet<Guid>> ChooseKeepersForPlayer(
        Player player, GameState state, CancellationToken ct)
    {
        var keepers = new HashSet<Guid>();

        foreach (var cardType in TypeOrder)
        {
            // Get cards of this type that haven't already been chosen as a keeper
            // (handles dual-type cards like enchantment creatures)
            var candidates = player.Battlefield.Cards
                .Where(c => c.CardTypes.HasFlag(cardType) && !keepers.Contains(c.Id))
                .ToList();

            if (candidates.Count == 0)
                continue;

            if (candidates.Count == 1)
            {
                // Auto-keep the only card of this type
                keepers.Add(candidates[0].Id);
                state.Log($"{player.Name} keeps {candidates[0].Name} ({cardType}).");
            }
            else
            {
                // Player chooses which one to keep
                var chosenId = await player.DecisionHandler.ChooseCard(
                    candidates,
                    $"Cataclysm: Choose one {cardType.ToString().ToLowerInvariant()} to keep.",
                    optional: false, ct);

                if (chosenId.HasValue && candidates.Any(c => c.Id == chosenId.Value))
                {
                    keepers.Add(chosenId.Value);
                    var chosen = candidates.First(c => c.Id == chosenId.Value);
                    state.Log($"{player.Name} keeps {chosen.Name} ({cardType}).");
                }
                else
                {
                    // Fallback: keep the first if the choice was invalid
                    keepers.Add(candidates[0].Id);
                    state.Log($"{player.Name} keeps {candidates[0].Name} ({cardType}).");
                }
            }
        }

        return keepers;
    }

    private static void SacrificeNonKeepers(Player player, HashSet<Guid> keepers,
        GameState state, StackObject spell)
    {
        var toSacrifice = player.Battlefield.Cards
            .Where(c => !keepers.Contains(c.Id))
            .ToList();

        foreach (var card in toSacrifice)
        {
            player.Battlefield.RemoveById(card.Id);
            player.Graveyard.Add(card);
            state.Log($"{player.Name} sacrifices {card.Name} ({spell.Card.Name}).");
        }
    }
}
