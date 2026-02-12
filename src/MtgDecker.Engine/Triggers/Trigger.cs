using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Triggers;

public record Trigger(GameEvent Event, TriggerCondition Condition, IEffect Effect);
