using Contracts.Processing;
using DB.Conversion.Enums;

namespace DB.Data.List;

internal record FilterGroup
{
	public List<string> Predicates { get; init; } = [];
	public List<FilterGroup> Groups { get; init; } = [];

	[EnumAsString]
	public FilterOperator Operator { get; init; }
}
