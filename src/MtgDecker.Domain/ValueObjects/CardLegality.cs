using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.ValueObjects;

public class CardLegality : IEquatable<CardLegality>
{
    public string FormatName { get; private set; }
    public LegalityStatus Status { get; private set; }

    public CardLegality(string formatName, LegalityStatus status)
    {
        FormatName = formatName;
        Status = status;
    }

    private CardLegality() { FormatName = string.Empty; }

    public bool Equals(CardLegality? other)
    {
        if (other is null) return false;
        return FormatName == other.FormatName && Status == other.Status;
    }

    public override bool Equals(object? obj) => Equals(obj as CardLegality);

    public override int GetHashCode() => HashCode.Combine(FormatName, Status);

    public static bool operator ==(CardLegality? left, CardLegality? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(CardLegality? left, CardLegality? right) => !(left == right);
}
