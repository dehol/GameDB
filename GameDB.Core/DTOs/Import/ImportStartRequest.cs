namespace GameDB.Core.DTOs.Import;

public record ImportStartRequest
{
    public int? Limit { get; init; }
    public List<int>? IgdbGameIds { get; init; }
    public bool OverwriteExisting { get; init; }
}
