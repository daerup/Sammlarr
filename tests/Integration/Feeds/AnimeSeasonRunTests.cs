using System.Net;
using Contracts.Datasets;
using Contracts.Feed;
using Core.Datasets;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class AnimeSeasonRunTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Season = "anime/season/2026/fall";

	private const string MapKnowingOnlyItsAnimeIds = """[{"type":"TV","anilist_id":185660,"mal_id":59130}]""";

	private const string NoPrequel = """{"data":{"Page":{"media":[{"idMal":59130,"format":"TV","relations":{"edges":[]}}]}}}""";

	private static readonly FeedItemSpec LaterSeason = new(
		"Kusuriya no Hitorigoto 3rd Season",
		MediaType.Show,
		2026,
		new Dictionary<MediaProvider, string> { [MediaProvider.Mal] = "59130" }
	);

	private IFeedRunEngine Testee => host.Resolve<IFeedRunEngine>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task Run_ForASeasonTitleNoServiceKnowsWhileTraktIsRateLimited_CountsItNotFound()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.WithMalAccount("mal-client-id");
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		await host.WithTraktAccount("trakt-client-id");
		host.MalSeasonHolds(Season, LaterSeason);
		await LoadAnimeMapAsync(MapKnowingOnlyItsAnimeIds);
		host.HttpStub.RespondWithJsonForUrl(TestUrls.AniList, NoPrequel);
		await host.RateLimitedAsync(RemoteService.Trakt, 5);
		var feedId = await SeedSeasonFeedAsync();

		var result = await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var completed = await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(completed!.Conclusiveness).IsEqualTo(Conclusiveness.NoMatches);
		var counts = (await host.Resolve<IFeedRepository>().GetRunsAsync(RunQuery.Take(1) with { Feed = Some.Of(feedId) })).Single().Counts;
		await Assert.That((counts.UnmatchedNotFound, counts.UnmatchedCouldNotLookUp)).IsEqualTo((1, 0));
		await Assert.That(host.HttpStub.Requests(TestUrls.AniList)).IsEqualTo(1);
	}

	[Test]
	public async Task Run_WhenAniListCannotSayWhatCameBefore_CountsItCouldNotLookUp()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.WithMalAccount("mal-client-id");
		host.MalSeasonHolds(Season, LaterSeason);
		await LoadAnimeMapAsync(MapKnowingOnlyItsAnimeIds);
		host.HttpStub.RespondWithStatusForUrl(TestUrls.AniList, HttpStatusCode.InternalServerError);
		var feedId = await SeedSeasonFeedAsync();

		var result = await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var completed = await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(completed!.Conclusiveness).IsEqualTo(Conclusiveness.Unresolved);
		var counts = (await host.Resolve<IFeedRepository>().GetRunsAsync(RunQuery.Take(1) with { Feed = Some.Of(feedId) })).Single().Counts;
		await Assert.That((counts.UnmatchedNotFound, counts.UnmatchedCouldNotLookUp)).IsEqualTo((0, 1));
		var trace = await host.Resolve<IFeedRunTraces>().ReadAsync(feedId, TraceCursor.Start);
		var snapshot = await Assert.That(trace).IsOfUnionType<TraceRead, TraceSnapshot>();
		await Assert.That(snapshot!.Fates.CouldNotLookUp).IsEqualTo(1);
	}

	private async Task LoadAnimeMapAsync(string map)
	{
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.AnimeLists}/anime-list-full.json", map);
		await host.Resolve<IEnumerable<IDatasetLoader>>().Single(loader => loader.Name == DatasetNames.AnimeIds).LoadAsync();
	}

	private async Task<FeedId> SeedSeasonFeedAsync()
	{
		var entity = new FeedRow
		{
			Name = "This season on MyAnimeList",
			IsEnabled = true,
			Provider = FeedProvider.Mal,
			Strategy = new Append(),
			Identifier = Season,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
