using DB.Conventions;

namespace DB.Data.Entities;

public record FilterGroup
{
	public List<string> Predicates { get; init; } = [];
	public List<FilterGroup> Groups { get; init; } = [];

	[EnumAsString]
	public ListFilterLogic Logic { get; init; }
}
