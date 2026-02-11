using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine;

public record TargetInfo(Guid CardId, Guid PlayerId, ZoneType Zone);
