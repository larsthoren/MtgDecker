namespace MtgDecker.Engine.Triggers.Effects;

/// <summary>
/// Kaito's +1 ability: You get an emblem with "Ninjas you control get +1/+1."
/// Creates a ContinuousEffect with ControllerOnly that applies to creatures
/// with the Ninja subtype, granting +1/+1.
/// </summary>
public class CreateNinjaEmblemEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var emblem = new Emblem(
            "Ninjas you control get +1/+1.",
            new ContinuousEffect(
                Guid.Empty,
                ContinuousEffectType.ModifyPowerToughness,
                (card, _) => card.Subtypes.Contains("Ninja"),
                PowerMod: 1,
                ToughnessMod: 1,
                ControllerOnly: true));

        context.Controller.Emblems.Add(emblem);
        context.State.Log($"{context.Controller.Name} gets an emblem: \"Ninjas you control get +1/+1.\"");

        return Task.CompletedTask;
    }
}
