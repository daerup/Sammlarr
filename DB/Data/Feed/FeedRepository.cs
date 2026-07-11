using System.Linq.Expressions;
using Contracts.Feed;
using DB.Data.Watchlist;
using Domain.Feed;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Feed;

internal sealed class FeedRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IFeedRepository
{
	private static readonly Expression<Func<FeedRow, Domain.Feed.Feed>> project = projection => new Domain.Feed.Feed
	{
		Id = FeedId.From(projection.Id),
		Name = projection.Name,
		Enabled = projection.IsEnabled,
		Provider = projection.Provider,
		Strategy = projection.Strategy,
		Identifier = projection.Identifier,
		CreatedAt = projection.CreatedAt,
		Filter = projection.Filter,
		Sort = projection.Sort,
		Limit = projection.Limit,
		Schedule = projection.SyncMode,
	};

	public async Task<IReadOnlyList<Domain.Feed.Feed>> GetFeedsAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Feeds.AsNoTracking().OrderByDescending(l => l.CreatedAt).Select(project).ToListAsync(cancellationToken);
	}

	public async Task<Domain.Feed.Feed?> GetFeedByIdAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return await ctx.Feeds.AsNoTracking().Where(l => l.Id == id.Value).Select(project).FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<FeedId> CreateFeedAsync(string name, FeedCreateData createData, CancellationToken cancellationToken = default)
	{
		var entity = new FeedRow
		{
			Name = name,
			IsEnabled = createData.IsEnabled,
			Provider = createData.Provider,
			Strategy = createData.Strategy,
			Identifier = createData.Identifier,
			Filter = createData.Filter,
			Sort = createData.Sort,
			Limit = createData.Limit,
			SyncMode = createData.Schedule,
		};

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		ctx.Feeds.Add(entity);
		await ctx.SaveChangesAsync(cancellationToken);
		return FeedId.From(entity.Id);
	}

	public async Task UpdateFeedAsync(FeedId id, FeedUpdateData updateData, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx.Feeds.FindAsync([id.Value], cancellationToken);
		if (entity == null)
		{
			throw new InvalidOperationException($"Feed with id {id} not found.");
		}

		entity.Name = updateData.Name;
		entity.IsEnabled = updateData.IsEnabled;
		entity.Provider = updateData.Provider;
		entity.Strategy = updateData.Strategy;
		entity.Identifier = updateData.Identifier;
		entity.Filter = updateData.Filter;
		entity.Sort = updateData.Sort;
		entity.Limit = updateData.Limit;
		entity.SyncMode = updateData.Schedule;

		await ctx.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteFeedAsync(FeedId id, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var entity = await ctx.Feeds.FindAsync([id.Value], cancellationToken);
		if (entity == null)
		{
			return;
		}

		await ctx
			.Watchlist.Where(w => w.SourceFeedId == id.Value)
			.ExecuteUpdateAsync(
				s => s.SetProperty(w => w.Origin, WatchlistEntryOrigin.Orphaned).SetProperty(w => w.SourceFeedNameSnapshot, entity.Name),
				cancellationToken
			);

		ctx.Feeds.Remove(entity);
		await ctx.SaveChangesAsync(cancellationToken);
	}
}
