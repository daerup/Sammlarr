using System.ComponentModel;

namespace Domain.Processing;

public record QuerySort
{
	public required string Property { get; init; }
	public required ListSortDirection Direction { get; init; }
}
