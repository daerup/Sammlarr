using Contracts.Processing;

namespace Contracts.Media.List;

public record ImportListCreateData
{
	public required bool IsEnabled { get; init; }
	public required MediaListProvider Provider { get; init; }
	public required MediaListAddStrategy Strategy { get; init; }
	public required string ListIdentifier { get; init; }
	public QueryFilter? Filter { get; init; }
	public QuerySort? Sort { get; init; }
	public QueryLimit? Limit { get; init; }
}
