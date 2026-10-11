using System.Net;
using Contracts.Feed;
using Contracts.Media;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class CachedRunTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_WhenTmdbAnswers503_RecordsAFailureWithBackoff()
	{
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feedId = await SeedFeedAsync("cached-list");
		host.MdblistFeedContains("cached-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "Fight Club")
			.ServedByTmdb(title: "Fight Club")
			.SeedWithDetailsAsync(title: "Fight Club", fetched: Now.AddDays(-15));
		host.TmdbDetailFails(TestMedia.FightClub, HttpStatusCode.ServiceUnavailable);

		await host.Resolve<IFeedRunEngine>().RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var stored = (await host.Resolve<IMediaRepository>().GetAsync([fightClub])).Single();
		var cached = await Assert.That(stored.Cached).IsSome();
		var answer = cached!.Answers.Single(each => each.Provider == RemoteService.Tmdb);
		await Assert.That(answer.Failures).IsEqualTo(1);
		await Assert.That(answer.AskableAt).IsEqualTo(Now.AddHours(1));
	}

	private static FeedItemSpec Item(string title, MediaType type, params ProviderId[] ids)
	{
		return new FeedItemSpec(title, type, null, ids.ToDictionary(id => id.Provider, id => id.Value));
	}

	private async Task<FeedId> SeedFeedAsync(string identifier)
	{
		var entity = new FeedRow
		{
			Name = "Comedy",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
