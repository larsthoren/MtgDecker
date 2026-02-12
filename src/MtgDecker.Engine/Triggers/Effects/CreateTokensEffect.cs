using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers.Effects;

public class CreateTokensEffect(
    string name, int power, int toughness, CardType cardTypes,
    IReadOnlyList<string> subtypes, int count = 1) : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            var token = new GameCard
            {
                Name = name,
                Power = power,
                Toughness = toughness,
                CardTypes = cardTypes,
                Subtypes = subtypes,
                IsToken = true,
                TurnEnteredBattlefield = context.State.TurnNumber,
            };
            context.Controller.Battlefield.Add(token);
            context.State.Log($"{context.Controller.Name} creates a {name} token ({power}/{toughness}).");
        }
        return Task.CompletedTask;
    }
}
