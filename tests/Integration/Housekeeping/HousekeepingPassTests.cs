using Contracts.Housekeeping;
using Contracts.Usage;
using Core.Housekeeping;
using Domain.Jobs;
using Domain.Providers;
using Domain.Usage;
using Harness;
using TestInfrastructure;
using TickerQ.Utilities.Enums;
using TUnit.Assertions.Enums;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class HousekeepingPassTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2024, 6, 1, 3, 0, 0, TimeSpan.Zero);

	private HousekeepingPass Testee => host.Resolve<HousekeepingPass>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_WithNobodySignedIn_RemovesFromEveryStepOnceThenReportsZero()
	{
		host.SetCurrentTime(Now);
		var usage = host.Resolve<IUsageStore>();
		await usage.AddAsync(Calls(Now.AddDays(-181)));
		await usage.AddAsync(Calls(Now.AddDays(-1)));
		await host.Movie(TestMedia.Inception).SeedWithDetailsAsync("Inception", Now.AddDays(-200));
		await host.AddedAsync(await host.Movie(TestMedia.FightClub).SeedAsync(), Now.AddDays(-400));
		await host.SeedRunAsync(await host.SeedFeedAsync(), Now.AddDays(-91), Guid.NewGuid());
		await host.SeedJobAsync(JobState.Done, Now.AddDays(-31));
		await host.SeedOccurrenceAsync(await host.SeedTickerAsync(), TickerStatus.Done, Now.AddDays(-31));
		await host.WithMdblistAccount("valid-mdblist-key");
		host.MdblistMaps(("15739", "tt0083564"));
		await host.StoredTwiceAsync("15739", "tt0083564");
		host.SignOut();

		var first = await Testee.RunAsync();
		var second = await Testee.RunAsync();

		await Assert.That(first.Failure).IsNone();
		await Assert.That(first.Steps.Select(step => step.Step)).IsEquivalentTo(Enum.GetValues<HousekeepingStep>(), CollectionOrdering.Matching);
		await Assert.That(first.Steps.Where(step => step.Removed == 0)).IsEmpty();
		await Assert.That(second.Steps.Sum(step => step.Removed)).IsEqualTo(0);
		await Assert.That(second.File).IsSome();
		var kept = await usage.ReadAsync(Now.AddDays(-200), Now);
		await Assert.That(kept.Calls).Count().IsEqualTo(1);
	}

	[Test]
	public async Task RunAsync_StoresItsReportAsTheLastOne()
	{
		host.SetCurrentTime(Now);

		var report = await Testee.RunAsync();

		await Assert.That(await host.Resolve<IHousekeepingReports>().LastAsync()).IsSome().And.IsEquivalentTo(report);
	}

	private static UsageRows Calls(DateTimeOffset hour)
	{
		return new UsageRows([new UsageCalls(RemoteService.Tmdb, hour, UsageCause.FeedRun, UsageOutcome.Answered, new None(), 1)], []);
	}
}
