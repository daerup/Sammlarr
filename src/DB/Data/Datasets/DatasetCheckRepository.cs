using Contracts.Datasets;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Datasets;

internal sealed class DatasetCheckRepository(IDbContextFactory<SammlarrDbContext> contexts) : IDatasetChecks
{
	public async Task RecordAsync(DatasetCheck check, CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
		await ctx.DatasetChecks.Where(row => row.Name == check.Name).ExecuteDeleteAsync(cancellationToken);
		ctx.DatasetChecks.Add(
			new DatasetCheckRow
			{
				Name = check.Name,
				StartedAt = check.StartedAt,
				FinishedAt = check.FinishedAt,
				Outcome = check.Outcome,
				Failure = check.Failure.ToNullable(),
			}
		);
		await ctx.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
	}

	public async Task<Optional<DatasetCheck>> FindAsync(string name, CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var row = await ctx.DatasetChecks.AsNoTracking().SingleOrDefaultAsync(check => check.Name == name, cancellationToken);
		return Optional.Of(row).Map(found => new DatasetCheck(found.Name, found.StartedAt, found.FinishedAt, found.Outcome, Optional.Of(found.Failure)));
	}
}
