namespace MtgDecker.Engine.AI;

public static class BoardEvaluator
{
    private const double LifeWeight = 1.0;
    private const double CreaturePowerWeight = 2.0;
    private const double CreatureToughnessWeight = 0.5;
    private const double CardInHandWeight = 1.5;
    private const double UntappedLandWeight = 0.3;
    private const double CreatureCountWeight = 0.5;

    public static double Evaluate(GameState state, Player player)
    {
        var opponent = state.GetOpponent(player);
        return ScorePlayer(player) - ScorePlayer(opponent);
    }

    private static double ScorePlayer(Player player)
    {
        double score = 0;

        score += player.Life * LifeWeight;

        var creatures = player.Battlefield.Cards.Where(c => c.IsCreature).ToList();
        score += creatures.Sum(c => (c.Power ?? 0) * CreaturePowerWeight);
        score += creatures.Sum(c => (c.Toughness ?? 0) * CreatureToughnessWeight);
        score += creatures.Count * CreatureCountWeight;

        score += player.Hand.Count * CardInHandWeight;

        var untappedLands = player.Battlefield.Cards.Count(c => c.IsLand && !c.IsTapped);
        score += untappedLands * UntappedLandWeight;

        return score;
    }
}
