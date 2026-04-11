using DB.Conversion.Enums;

namespace DB.Data.List;

internal record ListFilter
{
	public required List<FilterGroup> Groups { get; init; } = [];

	[EnumAsString]
	public required ListFilterLogic Logic { get; init; }
}
