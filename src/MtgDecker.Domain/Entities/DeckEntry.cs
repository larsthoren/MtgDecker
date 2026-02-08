using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.Entities;

public class DeckEntry
{
    public Guid Id { get; set; }
    public Guid DeckId { get; set; }
    public Guid CardId { get; set; }
    public int Quantity { get; set; }
    public DeckCategory Category { get; set; }
}
