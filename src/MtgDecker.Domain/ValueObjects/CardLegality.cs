using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.ValueObjects;

public class CardLegality
{
    public string FormatName { get; set; } = string.Empty;
    public LegalityStatus Status { get; set; }

    public CardLegality() { }

    public CardLegality(string formatName, LegalityStatus status)
    {
        FormatName = formatName;
        Status = status;
    }
}
