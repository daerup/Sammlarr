namespace DB.Data.Entities;

public record FilterGroup
{
    public List<string> Predicates { get; init; } = [];
    public List<FilterGroup> Groups { get; init; } = [];
    public ListFilterLogic Logic { get; init; }
}