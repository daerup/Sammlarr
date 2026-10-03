using Contracts.Ratings;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Ratings;

internal sealed class RatingDefaultRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IRatingDefaultRepository
{
	public async Task<Optional<RatingStrategy>> FindAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return Optional.Of(await ctx.RatingDefaults.AsNoTracking().SingleOrDefaultAsync(cancellationToken)).Map(row => row.Strategy);
	}

	public async Task StoreAsync(RatingStrategy strategy, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var stored = await ctx.RatingDefaults.SingleOrDefaultAsync(cancellationToken);
		if (stored is null)
		{
			ctx.RatingDefaults.Add(new RatingDefaultRow { Strategy = strategy });
		}
		else
		{
			stored.Strategy = strategy;
		}

		await ctx.SaveChangesAsync(cancellationToken);
	}
}
