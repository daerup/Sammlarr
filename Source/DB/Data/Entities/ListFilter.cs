namespace DB.Data.Entities;

public record ListFilter
{
    public required List<FilterGroup> Groups { get; init; } = [];
    public required ListFilterLogic Logic { get; init; }
}