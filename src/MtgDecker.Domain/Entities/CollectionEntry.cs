using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.Entities;

public class CollectionEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CardId { get; set; }
    public int Quantity { get; set; }
    public bool IsFoil { get; set; }
    public CardCondition Condition { get; set; } = CardCondition.NearMint;
}
