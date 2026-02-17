using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public record AlternateCost(
    int LifeCost = 0,
    ManaColor? ExileCardColor = null,
    string? ReturnLandSubtype = null,
    string? SacrificeLandSubtype = null,
    int SacrificeLandCount = 0,
    string? RequiresControlSubtype = null);
