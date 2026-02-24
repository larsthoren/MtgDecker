using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public record AlternateCost(
    int LifeCost = 0,
    ManaColor? ExileCardColor = null,
    string? ReturnLandSubtype = null,
    int ReturnLandCount = 1,
    string? SacrificeLandSubtype = null,
    int SacrificeLandCount = 0,
    string? RequiresControlSubtype = null,
    string? RequiresOpponentSubtype = null,
    string? DiscardLandSubtype = null,
    int DiscardAnyCount = 0,
    int ExileFromGraveyardCount = 0,
    ManaColor? ExileFromGraveyardColor = null);
