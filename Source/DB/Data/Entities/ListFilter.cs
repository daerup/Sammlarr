using DB.Conventions;

namespace DB.Data.Entities;

public record ListFilter
{
	public required List<FilterGroup> Groups { get; init; } = [];

	[EnumAsString]
	public required ListFilterLogic Logic { get; init; }
}
