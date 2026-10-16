using Contracts.Housekeeping;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class HousekeepingReportRepositoryTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Started = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);

	private IHousekeepingReports Testee => host.Resolve<IHousekeepingReports>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task LastAsync_BeforeAnyPass_IsNone()
	{
		await Assert.That(await Testee.LastAsync()).IsNone();
	}

	[Test]
	public async Task LastAsync_AfterAFinishedPass_ReadsItBackWhole()
	{
		var finished = new HousekeepingReport(
			Started,
			Some.Of(Started.AddSeconds(4)),
			[new StepCount(HousekeepingStep.RunHistory, 500), new StepCount(HousekeepingStep.CachedDetails, 96)],
			Some.Of(new Compaction(34_000_000, 32_700_000, true)),
			new None()
		);

		await Testee.SaveAsync(finished);

		await Assert.That(await Testee.LastAsync()).IsSome().And.IsEquivalentTo(finished);
	}

	[Test]
	public async Task SaveAsync_ForAFailedPassAfterARunningOne_KeepsOnlyTheFailedOne()
	{
		var failed = new HousekeepingReport(
			Started,
			Some.Of(Started.AddSeconds(2)),
			[new StepCount(HousekeepingStep.RunHistory, 500)],
			new None(),
			Some.Of(new HousekeepingFailure(Some.Of(HousekeepingStep.CachedDetails), "database is locked"))
		);
		await Testee.SaveAsync(new HousekeepingReport(Started, new None(), [], new None(), new None()));

		await Testee.SaveAsync(failed);

		await Assert.That(await Testee.LastAsync()).IsSome().And.IsEquivalentTo(failed);
	}
}
