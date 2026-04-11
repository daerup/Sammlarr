namespace DB.Data.Entities;

public record ListFilterAction
{
    public string Predicate { get; init; } = string.Empty;
}