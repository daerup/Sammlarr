using System.ComponentModel;
using DB.Conversion.Enums;

namespace DB.Data.List;

internal record ListSort
{
	public required string Property { get; init; }

	[EnumAsString]
	public required ListSortDirection Direction { get; init; }
}
