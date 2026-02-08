using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.Rules;

public static class FormatRules
{
    public static int GetMinDeckSize(Format format) => format switch
    {
        Format.Commander => 100,
        _ => 60
    };

    public static int? GetMaxDeckSize(Format format) => format switch
    {
        Format.Commander => 100,
        _ => null
    };

    public static int GetMaxCopies(Format format) => format switch
    {
        Format.Commander => 1,
        _ => 4
    };

    public static bool HasSideboard(Format format) => format switch
    {
        Format.Commander => false,
        _ => true
    };

    public static int GetMaxSideboardSize(Format format) => format switch
    {
        Format.Commander => 0,
        _ => 15
    };

    public static string GetScryfallName(Format format) => format switch
    {
        Format.Vintage => "vintage",
        Format.Legacy => "legacy",
        Format.Premodern => "premodern",
        Format.Modern => "modern",
        Format.Pauper => "pauper",
        Format.Commander => "commander",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
