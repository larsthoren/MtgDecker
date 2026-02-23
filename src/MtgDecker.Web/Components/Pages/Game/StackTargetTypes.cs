namespace MtgDecker.Web.Components.Pages.Game;

public record TargetHighlight(Guid CardId, Guid PlayerId, TargetZone Zone);

public enum TargetZone { Battlefield, Stack, Player }

public record StackTargetHoverInfo(string SourceElementId, List<TargetHighlight> Targets);
