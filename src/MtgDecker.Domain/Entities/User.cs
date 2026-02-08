namespace MtgDecker.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
