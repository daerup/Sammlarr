using Contracts;
using Contracts.Housekeeping;
using Core.Jobs;
using Microsoft.Extensions.Logging;

namespace Core.Housekeeping;

internal sealed class HousekeepingPass(
	IEnumerable<IHousekeepingStep> steps,
	IDatabaseFile database,
	IHousekeepingReports reports,
	JobTurn turn,
	TaskActivity activity,
	IDateTimeProvider clock,
	ILogger<HousekeepingPass> logger
)
{
	public async Task<HousekeepingReport> RunAsync(CancellationToken cancellationToken = default)
	{
		using var held = await turn.TakeAsync(cancellationToken);
		var running = new HousekeepingReport(clock.Now, new None(), [], new None(), new None());
		await reports.SaveAsync(running, cancellationToken);
		activity.Progressed();
		var finished = await RunStepsAsync(running, cancellationToken);
		await reports.SaveAsync(finished, cancellationToken);
		return finished;
	}

	private async Task<HousekeepingReport> RunStepsAsync(HousekeepingReport running, CancellationToken cancellationToken)
	{
		List<StepCount> counts = [];
		Optional<HousekeepingStep> at = new None();
		try
		{
			foreach (var step in steps.OrderBy(step => step.Step))
			{
				at = Some.Of(step.Step);
				var removed = await step.RunAsync(running.StartedAt, cancellationToken);
				logger.LogInformation("Housekeeping removed {Removed} rows in {Step}", removed, step.Step);
				counts.Add(new StepCount(step.Step, removed));
				await reports.SaveAsync(running with { Steps = [.. counts] }, cancellationToken);
				activity.Progressed();
			}

			at = new None();
			var file = await database.CompactAsync(cancellationToken);
			return running with { FinishedAt = Some.Of(clock.Now), Steps = counts, File = Some.Of(file) };
		}
		catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogError(exception, "Housekeeping stopped at {Step}", at.Map(step => step.ToString()).GetValueOr("the compaction"));
			return running with { FinishedAt = Some.Of(clock.Now), Steps = counts, Failure = Some.Of(new HousekeepingFailure(at, exception.Message)) };
		}
	}
}
