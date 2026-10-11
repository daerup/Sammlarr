using Contracts.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace DB.Data.Housekeeping;

internal sealed class HousekeepingReportRepository(IDbContextFactory<SammlarrDbContext> contexts) : IHousekeepingReports
{
	private const int Last = 1;

	public async Task SaveAsync(HousekeepingReport report, CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
		await ctx.HousekeepingReports.Where(row => row.Id == Last).ExecuteDeleteAsync(cancellationToken);
		ctx.HousekeepingReports.Add(Row(report));
		await ctx.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
	}

	public async Task<Optional<HousekeepingReport>> LastAsync(CancellationToken cancellationToken = default)
	{
		await using var ctx = await contexts.CreateDbContextAsync(cancellationToken);
		var row = await ctx.HousekeepingReports.AsNoTracking().Include(report => report.Steps).SingleOrDefaultAsync(report => report.Id == Last, cancellationToken);
		return Optional.Of(row).Map(Report);
	}

	private static HousekeepingReportRow Row(HousekeepingReport report)
	{
		return new HousekeepingReportRow
		{
			Id = Last,
			StartedAt = report.StartedAt,
			FinishedAt = report.FinishedAt.ToNullable(),
			BytesBefore = report.File.Map(file => file.BytesBefore).ToNullable(),
			BytesAfter = report.File.Map(file => file.BytesAfter).ToNullable(),
			Vacuumed = report.File.Map(file => file.Vacuumed).ToNullable(),
			FailedStep = report.Failure.Map(failure => failure.Step).ToNullable(),
			FailureReason = report.Failure.Map(failure => failure.Reason).ToNullable(),
			Steps = [.. report.Steps.Select(step => new HousekeepingStepRow { Step = step.Step, Removed = step.Removed })],
		};
	}

	private static HousekeepingReport Report(HousekeepingReportRow row)
	{
		return new HousekeepingReport(
			row.StartedAt,
			Optional.Of(row.FinishedAt),
			[.. row.Steps.OrderBy(step => step.Step).Select(step => new StepCount(step.Step, step.Removed))],
			Optional.Of(row.BytesBefore).Map(before => new Compaction(before, row.BytesAfter.GetValueOrDefault(), row.Vacuumed.GetValueOrDefault())),
			Optional.Of(row.FailureReason).Map(reason => new HousekeepingFailure(Optional.Of(row.FailedStep), reason))
		);
	}
}
