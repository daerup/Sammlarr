using Contracts.Feed;
using DB;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunStartupTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Sweep_ForAPendingRunLeftByACrash_MarksItInterrupted()
	{
		await host.SignInAs("Crashed");
		var run = TestFeeds.Run(TestFeeds.Feed((await SeedFeedAsync()).Value), Guid.NewGuid(), Conclusiveness.Pending);
		await host.Resolve<IFeedRepository>().StartRunAsync(run);
		await host.SignInAs("Member");

		await ActivatorUtilities.CreateInstance<DbInitializer>(host.Resolve<IServiceProvider>()).StartAsync(CancellationToken.None);

		var swept = await host.Context.FeedRuns.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == run.Id.Value);
		await Assert.That(swept.Conclusiveness).IsEqualTo(Conclusiveness.Stopped);
		await Assert.That(swept.SourceFailure).IsEqualTo(FeedRun.Interrupted);
	}

	private async Task<FeedId> SeedFeedAsync()
	{
		var entity = new FeedRow
		{
			Name = "Comedy",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = "hdlists/comedy",
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
