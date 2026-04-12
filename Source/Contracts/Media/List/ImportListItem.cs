namespace Contracts.Media.List;

public record ImportListItem
{
	public required int Year { get; init; }
	public required string Title { get; init; }
	public required ImportList Source { get; init; }
	public required IMediaId WatchlistId { get; init; }
	public required IEnumerable<IMediaId> AdditionalIds { get; init; }
}
