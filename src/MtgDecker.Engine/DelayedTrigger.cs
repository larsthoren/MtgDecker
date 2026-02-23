using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine;

public record DelayedTrigger(GameEvent FireOn, IEffect Effect, Guid ControllerId, Guid? TargetCardId = null);
