using System.ComponentModel;

namespace Contracts.Processing;

public record QuerySort
{
	public required string Property { get; init; }
	public required ListSortDirection Direction { get; init; }
}
