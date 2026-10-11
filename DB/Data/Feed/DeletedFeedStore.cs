using Contracts.Feed;
using Domain.Feed;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed;

internal sealed class DeletedFeedStore(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IDeletedFeeds
{
	public async Task AddAsync(DeletedFeed feed, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		ctx.DeletedFeeds.Add(
			new DeletedFeedRow
			{
				FeedId = feed.Feed.Value,
				Name = feed.Name,
				Provider = feed.Provider,
				DeletedAt = feed.DeletedAt,
			}
		);
		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<DeletedFeed>> ReadAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var rows = await ctx.DeletedFeeds.AsNoTracking().ToListAsync(cancellationToken);
		return [.. rows.Select(row => new DeletedFeed(FeedId.From(row.FeedId), row.Name, row.Provider, row.DeletedAt))];
	}
}
