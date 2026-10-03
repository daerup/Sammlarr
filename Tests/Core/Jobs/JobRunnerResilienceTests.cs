using System.Diagnostics;
using Contracts;
using Contracts.Identity;
using Contracts.Jobs;
using Core.Jobs;
using Domain.Identity;
using Domain.Jobs;
using Domain.Wantlist.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Jobs;

public class JobRunnerResilienceTests
{
	[Test]
	public async Task ExecuteAsync_WhenStartupRecoveryFails_KeepsRunningAndCountsTheFailure()
	{
		var (runner, status) = BuildTestee(Unreachable());

		await runner.StartAsync(CancellationToken.None);
		await WaitForAsync(() => status.ConsecutiveFailures > 0);

		await Assert.That(status.IsRunning).IsTrue();
		await Assert.That(runner.ExecuteTask!.IsFaulted).IsFalse();
		await Assert.That(status.LastFailureAt).IsSome().And.IsEqualTo(DateTimeOffset.UnixEpoch);

		await runner.StopAsync(CancellationToken.None);
	}

	[Test]
	public async Task ExecuteAsync_BeforeItHasStarted_DoesNotReportItselfStopped()
	{
		var (_, status) = BuildTestee(Idle());

		await Assert.That(status.HasStopped).IsFalse();
	}

	[Test]
	public async Task ExecuteAsync_BeforeItHasStarted_HasNoFailureToDate()
	{
		var (_, status) = BuildTestee(Idle());

		await Assert.That(status.LastFailureAt).IsNone();
	}

	[Test]
	public async Task ExecuteAsync_WhenStopped_ReportsThatItStopped()
	{
		var (runner, status) = BuildTestee(Idle());

		await runner.StartAsync(CancellationToken.None);
		await WaitForAsync(() => status.IsRunning);

		await runner.StopAsync(CancellationToken.None);

		await Assert.That(status.HasStopped).IsTrue();
	}

	[Test]
	public async Task ExecuteAsync_WhenAWaitingJobComesDue_TakesItWithoutAnythingElseBeingEnqueued()
	{
		var released = false;
		var jobs = IJobRepository.Mock();
		jobs.RequeueRunningAsync(Any<DateTimeOffset>(), Any()).Returns(0);
		jobs.NextWaitingAsync(Any()).Returns(() => Optional.When(!released, () => DateTimeOffset.UnixEpoch + TimeSpan.FromMilliseconds(100)));
		jobs.ReleaseDueAsync(Any<DateTimeOffset>(), Any())
			.Returns(() =>
			{
				released = true;
				return 1;
			});
		jobs.TakeNextAsync(Any<DateTimeOffset>(), Any()).Returns(new None());
		var (runner, _) = BuildTestee(jobs);

		await runner.StartAsync(CancellationToken.None);
		await WaitForAsync(() => released);

		await runner.StopAsync(CancellationToken.None);
	}

	[Test]
	public async Task TryRunNextAsync_WhenRecordingTheOutcomeFails_SurfacesItSoTheFailureIsCounted()
	{
		var (runner, _) = BuildTestee(Unsettleable());

		await Assert.That(async () => await runner.TryRunNextAsync(CancellationToken.None)).Throws<InvalidOperationException>();
	}

	private static Mock<IJobRepository> Idle()
	{
		var jobs = IJobRepository.Mock();
		jobs.RequeueRunningAsync(Any<DateTimeOffset>(), Any()).Returns(0);
		jobs.TakeNextAsync(Any<DateTimeOffset>(), Any()).Returns(new None());
		jobs.NextWaitingAsync(Any()).Returns(new None());
		return jobs;
	}

	private static Mock<IJobRepository> Unreachable()
	{
		var jobs = IJobRepository.Mock();
		jobs.RequeueRunningAsync(Any<DateTimeOffset>(), Any()).Throws(Missing());
		jobs.TakeNextAsync(Any<DateTimeOffset>(), Any()).Throws(Missing());
		jobs.NextWaitingAsync(Any()).Throws(Missing());
		return jobs;
	}

	private static Mock<IJobRepository> Unsettleable()
	{
		var jobs = IJobRepository.Mock();
		jobs.RequeueRunningAsync(Any<DateTimeOffset>(), Any()).Returns(0);
		jobs.TakeNextAsync(Any<DateTimeOffset>(), Any())
			.Returns(Some.Of(new RunningJob(JobId.From(Guid.NewGuid()), new SyncWantlist(new ManualTrigger()), UserId.New())));
		jobs.NextWaitingAsync(Any()).Returns(new None());
		jobs.CompleteAsync(Any<JobId>(), Any<DateTimeOffset>(), Any()).Throws(new InvalidOperationException("database is locked"));
		jobs.FailAsync(Any<JobId>(), Any<DateTimeOffset>(), Any()).Throws(new InvalidOperationException("database is locked"));
		return jobs;
	}

	private static InvalidOperationException Missing() => new("SQLite Error 1: 'no such table: Jobs'.");

	private static async Task WaitForAsync(Func<bool> condition)
	{
		var started = Stopwatch.GetTimestamp();
		while (!condition() && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(10))
		{
			await Task.Delay(20);
		}

		await Assert.That(condition()).IsTrue();
	}

	private static (JobRunner Testee, JobRunnerStatus Status) BuildTestee(Mock<IJobRepository> jobs)
	{
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(DateTimeOffset.UnixEpoch);
		var userScope = IUserScope.Mock();
		userScope.AsAsync(Any<UserId>(), Any()).Returns(new NoopDisposable());

		var services = new ServiceCollection();
		services.AddSingleton(jobs.Object);
		services.AddSingleton(userScope.Object);
		services.AddSingleton<IJobHandler<SyncWantlist>, NoopSyncHandler>();
		var status = new JobRunnerStatus();

		var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
		var signal = new JobSignal();
		var runner = new JobRunner(
			scopes,
			signal,
			new JobAlarm(scopes, signal, clock.Object, NullLogger<JobAlarm>.Instance),
			status,
			clock.Object,
			NullLogger<JobRunner>.Instance
		);

		return (runner, status);
	}

	private sealed class NoopDisposable : IDisposable
	{
		public void Dispose() { }
	}

	private sealed class NoopSyncHandler : IJobHandler<SyncWantlist>
	{
		public Task HandleAsync(SyncWantlist request, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}
}
