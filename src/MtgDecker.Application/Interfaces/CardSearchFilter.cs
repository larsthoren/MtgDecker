namespace MtgDecker.Application.Interfaces;

public class CardSearchFilter
{
    public string? SearchText { get; set; }
    public string? Format { get; set; }
    public List<string>? Colors { get; set; }
    public string? Type { get; set; }
    public double? MinCmc { get; set; }
    public double? MaxCmc { get; set; }
    public string? Rarity { get; set; }
    public string? SetCode { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
