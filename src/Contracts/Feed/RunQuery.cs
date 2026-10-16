using Domain.Feed;
using Domain.Feed.Run;

namespace Contracts.Feed;

public sealed record RunQuery
{
	private RunQuery(int limit) => Limit = limit;

	public int Limit { get; }

	public Optional<FeedId> Feed { get; init; } = new None();

	public Optional<DateTimeOffset> Since { get; init; } = new None();

	public Optional<RunId> MustInclude { get; init; } = new None();

	public bool WithItems { get; init; } = true;

	public static RunQuery Take(int limit) => new(limit);
}
