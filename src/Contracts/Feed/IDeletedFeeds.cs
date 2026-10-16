using Domain.Feed;
using Domain.Providers;

namespace Contracts.Feed;

public sealed record DeletedFeed(FeedId Feed, string Name, FeedProvider Provider, DateTimeOffset DeletedAt);

public interface IDeletedFeeds
{
	Task AddAsync(DeletedFeed feed, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<DeletedFeed>> ReadAsync(CancellationToken cancellationToken = default);
}
