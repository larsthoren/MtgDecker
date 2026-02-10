namespace MtgDecker.Engine;

public class GameCard
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string TypeLine { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsTapped { get; set; }

    public bool IsLand => TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);
    public bool IsCreature => TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);
}
