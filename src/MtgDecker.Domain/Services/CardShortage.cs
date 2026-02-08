namespace MtgDecker.Domain.Services;

public record CardShortage(string CardName, int Needed, int Owned, int Shortage);
