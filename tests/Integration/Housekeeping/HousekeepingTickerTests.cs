using Contracts.Housekeeping;
using Core.Housekeeping;
using Core.Jobs;
using Harness;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class HousekeepingTickerTests(SammlarrTestHost host) : IAsyncDisposable
{
	private TaskActivity Activity => field ??= new TaskActivity(host.Resolve<TimeProvider>());

	private HousekeepingTicker Testee => new(host.Resolve<IServiceScopeFactory>(), Activity);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_WhenTheScheduleFires_KeepsThePassReportAndEndsTheRun()
	{
		await Testee.RunAsync(CancellationToken.None);

		await Assert.That(await host.Resolve<IHousekeepingReports>().LastAsync()).IsSome();
		await Assert.That(Activity.Running(HousekeepingTicker.FunctionName)).IsNone();
	}
}
