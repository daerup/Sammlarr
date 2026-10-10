using Contracts.Feed;
using Contracts.Media;
using Contracts.Wantlist;
using Core.Media;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunIdentityTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Remakes = """
		[{"title":"Annie","year":1982,"type":"movie","ids":{"imdb":"tt0083564","trakt":9600,"tmdb":15739,"tvdb":null,"mal":null,"mdblist":"2z2"},"ratings":[
		{"source":"metacritic","value":39,"score":39,"votes":10,"url":"/annie"}]},
		{"title":"Annie","year":1999,"type":"movie","ids":{"imdb":"tt0207972","trakt":15878,"tmdb":26177,"tvdb":null,"mal":null,"mdblist":"3j4j8"},"ratings":[
		{"source":"metacritic","value":61,"score":61,"votes":10,"url":"/annie"}]}]
		""";

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForTwoRemakesSharingAMetacriticSlug_AddsBothAndSavesTheRun()
	{
		await StubRemakesAsync();
		var feedId = await SeedRemakesFeedAsync();

		var result = await host.Resolve<IFeedRunEngine>().RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(await host.Resolve<IWantlistRepository>().GetAllAsync()).Count().IsEqualTo(2);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_ForTwoRemakesSharingAMetacriticSlug_HandsOutOnlyIdsThatAreStored()
	{
		await StubRemakesAsync();
		var requests = new[] { Request(TmdbId.From("15739", MediaType.Movie)), Request(TmdbId.From("26177", MediaType.Movie)) };

		var results = await host.Resolve<IMediaService>().IdentifyWithDetailsAsync(requests.ToAsyncEnumerable()).ToListAsync();

		await Assert.That(results.AsEnumerable()).AreOfUnionType<IEnumerable<MediaMatchResult>, MediaMatchResult>(typeof(MatchedMedia), typeof(MatchedMedia));
		var handedOut = HandedOut(results);
		await Assert.That(handedOut).Count().IsEqualTo(2);
		await Assert.That(await StoredAsync(handedOut)).IsEqualTo(2);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenALaterTitleTiesAnEarlierOneToAStoredRow_HandsOutOnlyIdsThatAreStored()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var tmdbIds = new[] { "1" }.Concat(Enumerable.Range(101, MediaService.FirstBatchSize - 1).Select(n => $"{n}")).ToList();
		var titles = string.Join(
			",",
			tmdbIds.Select(id => $$"""{"title":"T{{id}}","year":2000,"type":"movie","ids":{"imdb":"tt{{id}}","tmdb":{{id}}},"ratings":[]}""")
		);
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/imdb/movie", $"[{titles}]");
		await host.SeedMediaAsync(ImdbId.From("tt9", MediaType.Movie));
		var requests = tmdbIds
			.Select(id => Request(TmdbId.From(id, MediaType.Movie), ImdbId.From($"tt{id}", MediaType.Movie)))
			.Append(Request(TmdbId.From("1", MediaType.Movie), ImdbId.From("tt9", MediaType.Movie)))
			.ToList();

		var results = await host.Resolve<IMediaService>().IdentifyWithDetailsAsync(requests.ToAsyncEnumerable()).ToListAsync();

		var handedOut = HandedOut(results);
		await Assert.That(await StoredAsync(handedOut)).IsEqualTo(handedOut.Count);
	}

	private static IdentifyRequest Request(params IExternalMediaId[] ids) => new(ids, new None());

	private static List<Guid> HandedOut(IEnumerable<MediaMatchResult> results) =>
		[.. results.Select(result => result is MatchedMedia matched ? matched.Identity.Id.Value : (Guid?)null).OfType<Guid>().Distinct()];

	private async Task<int> StoredAsync(List<Guid> handedOut)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		return await host.ScopedDbContext.Media.Where(media => handedOut.Contains(media.Id)).CountAsync();
	}

	private async Task StubRemakesAsync()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.MdblistApi}/tmdb/movie", Remakes);
	}

	private async Task<FeedId> SeedRemakesFeedAsync()
	{
		var entity = new FeedRow
		{
			Name = "Remakes",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new Append(),
			Identifier = "remakes-list",
		};
		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		host.MdblistFeedContains(
			"remakes-list",
			new FeedItemSpec("Annie", MediaType.Movie, 1982, new Dictionary<MediaProvider, string> { [MediaProvider.Tmdb] = "15739" }),
			new FeedItemSpec("Annie", MediaType.Movie, 1999, new Dictionary<MediaProvider, string> { [MediaProvider.Tmdb] = "26177" })
		);
		return FeedId.From(entity.Id);
	}
}
