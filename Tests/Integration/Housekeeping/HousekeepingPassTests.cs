using Contracts.Housekeeping;
using Contracts.Usage;
using Core.Housekeeping;
using Domain.Providers;
using Domain.Usage;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class HousekeepingPassTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2024, 6, 1, 3, 0, 0, TimeSpan.Zero);

	private HousekeepingPass Testee => host.Resolve<HousekeepingPass>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_Twice_RemovesOnceThenReportsZero()
	{
		host.SetCurrentTime(Now);
		var usage = host.Resolve<IUsageStore>();
		await usage.AddAsync(Calls(Now.AddDays(-181)));
		await usage.AddAsync(Calls(Now.AddDays(-1)));

		var first = await Testee.RunAsync();
		var second = await Testee.RunAsync();

		await Assert.That(Removed(first, HousekeepingStep.UsageStats)).IsEqualTo(1);
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

	private static int Removed(HousekeepingReport report, HousekeepingStep step) => report.Steps.Single(count => count.Step == step).Removed;

	private static UsageRows Calls(DateTimeOffset hour)
	{
		return new UsageRows([new UsageCalls(RemoteService.Tmdb, hour, UsageCause.FeedRun, UsageOutcome.Answered, new None(), 1)], []);
	}
}
