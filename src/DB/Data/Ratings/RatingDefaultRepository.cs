using Contracts;
using Contracts.Ratings;
using Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Ratings;

internal sealed class RatingDefaultRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory, IDateTimeProvider clock) : IRatingDefaultRepository
{
	public async Task<Optional<RatingStrategy>> FindAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		return Optional.Of(await ctx.RatingDefaults.AsNoTracking().SingleOrDefaultAsync(cancellationToken)).Map(row => row.Strategy);
	}

	public async Task StoreAsync(RatingStrategy strategy, CancellationToken cancellationToken = default)
	{
		if (await UpdateAsync(strategy, cancellationToken) > 0)
		{
			return;
		}

		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		ctx.RatingDefaults.Add(new RatingDefaultRow { Strategy = strategy });
		try
		{
			await ctx.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException)
		{
			if (await UpdateAsync(strategy, cancellationToken) == 0)
			{
				throw;
			}
		}
	}

	private async Task<int> UpdateAsync(RatingStrategy strategy, CancellationToken cancellationToken)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var now = clock.Now;
		return await ctx.RatingDefaults.ExecuteUpdateAsync(
			setters => setters.SetProperty(row => row.Strategy, strategy).SetProperty(row => row.UpdatedAt, now),
			cancellationToken
		);
	}
}
