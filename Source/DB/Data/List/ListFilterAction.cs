namespace DB.Data.List;

internal record ListFilterAction
{
	public string Predicate { get; init; } = string.Empty;
}
