using Contracts.Usage;
using Domain.Feed;
using Domain.Providers;
using Domain.Usage;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Usage;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UsageStoreTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Hour = new(2024, 5, 31, 22, 0, 0, TimeSpan.Zero);

	private static readonly FeedId Feed = FeedId.From(Guid.NewGuid());

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task AddAsync_TwiceForOneHour_AddsUpItsRows()
	{
		var store = host.Resolve<IUsageStore>();

		await store.AddAsync(Rows(Hour, calls: 2));
		await store.AddAsync(Rows(Hour, calls: 3));

		var read = await store.ReadAsync(Hour, Hour.AddHours(1));
		await Assert.That(read.Calls.Select(row => row.Calls)).IsEquivalentTo([5L]);
		await Assert.That(read.Calls.Single().Feed).IsSome().And.IsEqualTo(Feed);
		await Assert.That(read.Attempts.Select(row => row.Attempts)).IsEquivalentTo([10L]);
		await Assert.That(read.Attempts.Single().Buckets).IsEquivalentTo(new Dictionary<int, long> { [100] = 8, [DurationBuckets.Over] = 2 });
	}

	[Test]
	public async Task ReadAsync_AWindowInLocalTime_ReadsTheSameHours()
	{
		var store = host.Resolve<IUsageStore>();
		var local = TimeSpan.FromHours(2);

		await store.AddAsync(Rows(Hour, calls: 1));

		var read = await store.ReadAsync(Hour.ToOffset(local), Hour.AddHours(1).ToOffset(local));
		await Assert.That(read.Calls).Count().IsEqualTo(1);
	}

	[Test]
	public async Task PruneAsync_DeletesTheRowsBeforeTheCutOff()
	{
		var store = host.Resolve<IUsageStore>();
		await store.AddAsync(Rows(Hour.AddDays(-91), calls: 1));
		await store.AddAsync(Rows(Hour, calls: 1));

		await store.PruneAsync(Hour.AddDays(-90));

		var read = await store.ReadAsync(Hour.AddDays(-100), Hour.AddHours(1));
		await Assert.That(read.Calls.Select(row => row.Hour)).IsEquivalentTo([Hour]);
		await Assert.That(read.Attempts.Select(row => row.Hour)).IsEquivalentTo([Hour]);
	}

	[Test]
	public async Task ReadAsync_ThroughTheReport_SplitsTheDayFromTheDayBefore()
	{
		var store = host.Resolve<IUsageStore>();
		await store.AddAsync(Rows(Hour, calls: 2));
		await store.AddAsync(Rows(Hour.AddDays(-1), calls: 5));

		var report = await host.Resolve<IUsageReport>().ReadAsync(UsageRange.Day);

		await Assert.That(report.Current.Tally.Total).IsEqualTo(2);
		await Assert.That(report.Previous.Tally.Total).IsEqualTo(5);
		await Assert.That(report.Current.Services.Single().Tally.Percentile(0.5)).IsSome();
	}

	private static UsageRows Rows(DateTimeOffset hour, long calls)
	{
		return new UsageRows(
			[new UsageCalls(RemoteService.Tmdb, hour, UsageCause.FeedRun, UsageOutcome.Answered, Some.Of(Feed), calls)],
			[
				new UsageAttempts(
					RemoteService.Tmdb,
					hour,
					UsageCause.FeedRun,
					Some.Of(Feed),
					calls * 2,
					new Dictionary<int, long> { [100] = (calls * 2) - 1, [DurationBuckets.Over] = 1 }
				),
			]
		);
	}
}
