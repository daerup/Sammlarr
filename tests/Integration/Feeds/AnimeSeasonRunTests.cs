using System.Net;
using System.Text;
using Contracts.Datasets;
using Contracts.Feed;
using Core.Datasets;
using DB.Data.Feed;
using DB.Data.Media;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Feed.Trace;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class AnimeSeasonRunTests(SammlarrTestHost host) : IAsyncDisposable
{
	private const string Season = "anime/season/2026/fall";

	private const string MapKnowingOnlyItsAnimeIds = """[{"type":"TV","anilist_id":185660,"mal_id":59130}]""";

	private const string NoPrequel = """{"data":{"Page":{"media":[{"idMal":59130,"format":"TV","relations":{"edges":[]}}]}}}""";

	private const string ShowAndItsBareSeason = """
		[{"type":"TV","anilist_id":161645,"mal_id":54492,"tvdb_id":431162,"season":{"tvdb":1}},
		{"type":"TV","anilist_id":185660,"mal_id":59130}]
		""";

	private const string ShowAndItsPlacedSeason = """
		[{"type":"TV","anilist_id":161645,"mal_id":54492,"tvdb_id":431162,"season":{"tvdb":1}},
		{"type":"TV","anilist_id":185660,"mal_id":59130,"tvdb_id":431162,"season":{"tvdb":3}}]
		""";

	private const string ThePrequelIsTheShow =
		"""{"data":{"Page":{"media":[{"idMal":59130,"format":"TV","relations":{"edges":[{"relationType":"PREQUEL","node":{"idMal":54492,"type":"ANIME","format":"TV"}}]}}]}}}""";

	private const string NoTitles = """{"data":{"Page":{"media":[]}}}""";

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

	[Test]
	public async Task Run_TwiceForALaterSeasonFoundThroughItsPrequel_AsksForThePrequelOnce()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.WithMalAccount("mal-client-id");
		host.MalSeasonHolds(Season, LaterSeason);
		await LoadAnimeMapAsync(ShowAndItsBareSeason);
		var prequelAsks = AnsweringAniList(ThePrequelIsTheShow);
		NoDetailsForTheShow();
		var feedId = await SeedSeasonFeedAsync();

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));
		var first = await PartsAsync();
		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(prequelAsks()).IsEqualTo(1);
		await Assert.That(first.Select(part => $"{part.Source}:{part.Id}")).IsEquivalentTo(["AniList:185660", "Mal:59130"]);
		await Assert.That(await PartsAsync()).IsEquivalentTo(first);
	}

	[Test]
	public async Task Run_ForALaterSeasonTheMapPointsAtItsShow_LinksItWithoutAPrequelAsk()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.WithMalAccount("mal-client-id");
		host.MalSeasonHolds(Season, LaterSeason);
		await LoadAnimeMapAsync(ShowAndItsPlacedSeason);
		var prequelAsks = AnsweringAniList(ThePrequelIsTheShow);
		NoDetailsForTheShow();
		var feedId = await SeedSeasonFeedAsync();

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(prequelAsks()).IsEqualTo(0);
		await Assert.That((await PartsAsync()).Select(part => $"{part.Id} season {part.TvdbSeason}")).IsEquivalentTo(["185660 season 3", "59130 season 3"]);
	}

	[Test]
	public async Task Run_WhenTheSeasonCouldNotBeLookedUp_LinksNothingAndAsksAgainNextRun()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.WithMalAccount("mal-client-id");
		host.MalSeasonHolds(Season, LaterSeason);
		await LoadAnimeMapAsync(ShowAndItsBareSeason);
		host.HttpStub.RespondWithStatusForUrl(TestUrls.AniList, HttpStatusCode.InternalServerError);
		var feedId = await SeedSeasonFeedAsync();

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));
		var firstRun = host.HttpStub.Requests(TestUrls.AniList);
		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		await Assert.That(await PartsAsync()).IsEmpty();
		await Assert.That(host.HttpStub.Requests(TestUrls.AniList)).IsGreaterThan(firstRun);
	}

	[Test]
	public async Task Run_TwiceForASeasonWithoutAPrequel_AsksForThePrequelOnce()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.WithMalAccount("mal-client-id");
		host.MalSeasonHolds(Season, LaterSeason);
		await LoadAnimeMapAsync(MapKnowingOnlyItsAnimeIds);
		var prequelAsks = AnsweringAniList(NoPrequel);
		var feedId = await SeedSeasonFeedAsync();

		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));
		await Testee.RunAsync(feedId, new FeedRunTrigger(new Manual()));

		var parts = await PartsAsync();
		await Assert.That(prequelAsks()).IsEqualTo(1);
		await Assert.That(parts.Select(part => $"{part.Source}:{part.Id}")).IsEquivalentTo(["AniList:185660", "Mal:59130"]);
		await Assert.That(parts.Where(part => part.Media != null)).IsEmpty();
	}

	private void NoDetailsForTheShow()
	{
		host.HttpStub.RespondWithStatusForUrl("/anime/54492", HttpStatusCode.NotFound);
		host.HttpStub.RespondWithStatusForUrl("/lookup/shows?thetvdb=431162", HttpStatusCode.NotFound);
	}

	private Func<int> AnsweringAniList(string prequels)
	{
		var asked = 0;
		host.HttpStub.RespondForUrlAsync(
			TestUrls.AniList,
			async request =>
			{
				var body = await request.Content!.ReadAsStringAsync();
				var isPrequelAsk = body.Contains("idMal_in", StringComparison.Ordinal);
				if (isPrequelAsk)
				{
					Interlocked.Increment(ref asked);
				}

				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(isPrequelAsk ? prequels : NoTitles, Encoding.UTF8, "application/json"),
				};
			}
		);
		return () => asked;
	}

	private async Task<IReadOnlyList<StoredPart>> PartsAsync()
	{
		await using var context = await host.ContextFactory.CreateDbContextAsync();
		return await context
			.ShowParts.AsNoTracking()
			.Select(part => new StoredPart(part.Source, part.Id, EF.Property<Guid?>(part, MediaQueries.MediaIdColumn), part.TvdbSeason))
			.ToListAsync();
	}

	private async Task LoadAnimeMapAsync(string map)
	{
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.AnimeLists}/anime-list-full.json", map);
		await host.Resolve<IEnumerable<IDatasetLoader>>().Single(loader => loader.Name == DatasetNames.AnimeIds).LoadAsync(DatasetRead.WhenChanged);
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

	private sealed record StoredPart(MediaProvider Source, string Id, Guid? Media, int? TvdbSeason);
}
