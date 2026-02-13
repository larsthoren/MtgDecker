using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Effects;

public class CreateTokenSpellEffect : SpellEffect
{
    public string TokenName { get; }
    public int Power { get; }
    public int Toughness { get; }
    public CardType CardTypes { get; }
    public IReadOnlyList<string> Subtypes { get; }
    public int Count { get; }

    public CreateTokenSpellEffect(string name, int power, int toughness, CardType cardTypes, IReadOnlyList<string> subtypes, int count = 1)
    {
        TokenName = name;
        Power = power;
        Toughness = toughness;
        CardTypes = cardTypes;
        Subtypes = subtypes;
        Count = count;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        var controller = state.GetPlayer(spell.ControllerId);
        for (int i = 0; i < Count; i++)
        {
            var token = new GameCard
            {
                Name = TokenName,
                Power = Power,
                Toughness = Toughness,
                CardTypes = CardTypes,
                Subtypes = Subtypes,
                IsToken = true,
                TurnEnteredBattlefield = state.TurnNumber,
            };
            controller.Battlefield.Add(token);
            state.Log($"{controller.Name} creates a {TokenName} token ({Power}/{Toughness}).");
        }
    }
}
