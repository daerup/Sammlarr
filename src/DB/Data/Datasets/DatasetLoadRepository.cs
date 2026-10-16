using Contracts.Datasets;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Datasets;

internal sealed class DatasetLoadRepository(IDbContextFactory<SammlarrDbContext> dbContextFactory) : IDatasetLoads
{
	public async Task<Optional<DatasetLoad>> FindAsync(string name, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.DatasetLoads.AsNoTracking().SingleOrDefaultAsync(load => load.Name == name, cancellationToken);
		return Optional.Of(row).Map(found => new DatasetLoad(found.Name, found.LoadedAt, Optional.Of(found.ETag), found.Rows));
	}

	public async Task RecordAsync(DatasetLoad load, CancellationToken cancellationToken = default)
	{
		await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
		var row = await ctx.DatasetLoads.SingleOrDefaultAsync(existing => existing.Name == load.Name, cancellationToken);
		var recorded =
			row
			?? ctx.DatasetLoads.Add(
				new DatasetLoadRow
				{
					Name = load.Name,
					LoadedAt = load.LoadedAt,
					Rows = load.Rows,
				}
			).Entity;
		recorded.LoadedAt = load.LoadedAt;
		recorded.ETag = load.ETag.ToNullable();
		recorded.Rows = load.Rows;
		await ctx.SaveChangesAsync(cancellationToken);
	}
}
