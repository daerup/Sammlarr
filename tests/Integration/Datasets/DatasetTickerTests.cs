using System.Net;
using Contracts;
using Contracts.Datasets;
using Core.Datasets;
using Core.Jobs;
using Harness;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;

namespace Integration.Tests.Datasets;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class DatasetTickerTests(SammlarrTestHost host) : IAsyncDisposable
{
	private TaskActivity Activity => field ??= new TaskActivity(host.Resolve<IDateTimeProvider>());

	private DatasetTicker Testee => new(host.Resolve<IServiceScopeFactory>(), Activity);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task LoadAsync_WhenTheDownloadFails_RecordsTheFailedCheckAndStopsRunning()
	{
		host.HttpStub.RespondForUrl($"{TestUrls.ImdbDatasets}/title.ratings.tsv.gz", _ => new HttpResponseMessage(HttpStatusCode.NotFound));

		await Testee.LoadAsync(DatasetNames.ImdbRatings);

		var check = await host.Resolve<IDatasetChecks>().FindAsync(DatasetNames.ImdbRatings);
		await Assert.That(check.Map(found => found.Outcome)).IsSome().And.IsEqualTo(DatasetOutcome.Failed);
		await Assert.That(check.Map(found => found.Failure)).IsSome();
		await Assert.That(Activity.Running(DatasetNames.ImdbRatings)).IsNone();
	}
}
