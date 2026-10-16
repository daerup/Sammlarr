using Contracts.Jobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Web.Health;

namespace Web.Tests.Health;

public class JobRunnerHealthCheckTests
{
	private static readonly DateTimeOffset FailedAt = new(2026, 10, 3, 9, 30, 0, TimeSpan.Zero);

	private readonly Mock<IJobRunnerStatus> _status = IJobRunnerStatus.Mock();

	private JobRunnerHealthCheck Testee => new(_status.Object);

	[Test]
	public async Task CheckHealthAsync_WithoutAFailure_IsHealthy()
	{
		_status.ConsecutiveFailures.Returns(0);
		_status.LastFailureAt.Returns(new None());

		var result = await Testee.CheckHealthAsync(new HealthCheckContext());

		await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
	}

	[Test]
	public async Task CheckHealthAsync_AfterAFewFailures_IsDegradedSinceTheLastOne()
	{
		_status.ConsecutiveFailures.Returns(2);
		_status.LastFailureAt.Returns(Some.Of(FailedAt));

		var result = await Testee.CheckHealthAsync(new HealthCheckContext());

		await Assert.That(result.Status).IsEqualTo(HealthStatus.Degraded);
		await Assert
			.That(result.Description)
			.IsEqualTo("The job runner has failed 2 time(s) in a row since 2026-10-03 09:30:00Z. It retries when work is enqueued.");
	}

	[Test]
	public async Task CheckHealthAsync_AfterTooManyFailures_IsUnhealthySinceTheLastOne()
	{
		_status.ConsecutiveFailures.Returns(6);
		_status.LastFailureAt.Returns(Some.Of(FailedAt));

		var result = await Testee.CheckHealthAsync(new HealthCheckContext());

		await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
		await Assert.That(result.Description).IsEqualTo("The job runner has failed 6 times in a row since 2026-10-03 09:30:00Z. Queued work is not running.");
	}
}
