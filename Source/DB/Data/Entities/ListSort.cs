using System.ComponentModel;
using DB.Conventions;

namespace DB.Data.Entities;

public record ListSort
{
	public required string Property { get; init; }

	[EnumAsString]
	public required ListSortDirection Direction { get; init; }
}
