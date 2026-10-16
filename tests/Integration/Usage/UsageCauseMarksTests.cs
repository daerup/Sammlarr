using Contracts.Jobs;
using Domain.Feed;
using Domain.Jobs;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Harness;
using Shared.Options;

namespace Integration.Tests.Usage;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class UsageCauseMarksTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Jobs_ForAFeedRunAndTheDetailsFillItArms_SendNoMdblistCallWithoutACause()
	{
		host.SetCurrentTime(Now);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feed = await host.SeedFeedAsync("marked-list");
		host.MdblistFeedContains(
			"marked-list",
			new FeedItemSpec("Fight Club", MediaType.Movie, 1999, new Dictionary<MediaProvider, string> { [MediaProvider.Tmdb] = "550" })
		);
		await host.Movie(TestMedia.FightClub).SeedAsync();
		await host.Resolve<IJobQueue>().EnqueueAsync(new RunFeed(feed, new Manual()));

		while (await host.RunNextJobAsync()) { }
		var due = Now + host.Resolve<SchedulerOptions>().FillDetailsGrace;
		host.SetCurrentTime(due);
		await host.Resolve<IJobRepository>().ReleaseDueAsync(due);
		while (await host.RunNextJobAsync()) { }

		var causes = host.HttpStub.Causes(TestUrls.MdblistApi);
		await Assert.That(causes).Contains(UsageCause.FeedRun).And.Contains(UsageCause.DetailsFill);
		await Assert.That(causes).DoesNotContain(UsageCause.Other);
	}
}
