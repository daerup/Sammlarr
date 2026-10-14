using Contracts.Jobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Web.Health;

internal sealed class JobRunnerLivenessCheck(IJobRunnerStatus status) : IHealthCheck
{
	public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
		Task.FromResult(
			status.HasStopped
				? HealthCheckResult.Unhealthy("The job runner stopped, so no queued work will run until the app restarts.")
				: HealthCheckResult.Healthy()
		);
}

internal sealed class JobRunnerHealthCheck(IJobRunnerStatus status) : IHealthCheck
{
	private const int ToleratedFailures = 5;

	public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) => Task.FromResult(Evaluate());

	private HealthCheckResult Evaluate()
	{
		var failures = status.ConsecutiveFailures;
		var since = status.LastFailureAt.Map(at => $" since {at:u}").GetValueOr(string.Empty);

		return failures switch
		{
			0 => HealthCheckResult.Healthy(),
			> ToleratedFailures => HealthCheckResult.Unhealthy($"The job runner has failed {failures} times in a row{since}. Queued work is not running."),
			_ => HealthCheckResult.Degraded($"The job runner has failed {failures} time(s) in a row{since}. It retries when work is enqueued."),
		};
	}
}
