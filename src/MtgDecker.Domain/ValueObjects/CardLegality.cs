using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.ValueObjects;

public class CardLegality
{
    public string FormatName { get; private set; }
    public LegalityStatus Status { get; private set; }

    public CardLegality(string formatName, LegalityStatus status)
    {
        FormatName = formatName;
        Status = status;
    }

    private CardLegality() { FormatName = string.Empty; }
}
