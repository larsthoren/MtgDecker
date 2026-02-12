namespace MtgDecker.Engine;

public interface IStackObject
{
    Guid Id { get; }
    Guid ControllerId { get; }
}
