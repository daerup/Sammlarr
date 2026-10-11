using System.Net;
using API.Http.Lanes;
using API.Mdblist;
using API.Plex;
using API.Tmdb;
using Contracts.Feed;
using Contracts.Media;
using DB.Data.Feed;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Harness;
using Shared.Concurrency;
using Shared.Options;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class CachedRunTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset NextDay = Now.AddDays(1);
	private const string TmdbList = "42";

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

		var answer = (await StoredAsync(fightClub)).Answers.Single(each => each.Provider == RemoteService.Tmdb);
		await Assert.That(answer.Failures).IsEqualTo(1);
		await Assert.That(answer.AskableAt).IsEqualTo(Now.AddHours(1));
	}

	[Test]
	public async Task RunAsync_WhenTheFeedReadsNoMissingField_MakesNoTitleCalls()
	{
		await ConnectTmdbAndPlexAsync();
		var details = Complete("Fight Club") with { Genres = [] };
		await StoreAsync(TestMedia.FightClub, details, RemoteService.Tmdb);
		var feedId = await SeedTmdbListAsync(TestMedia.FightClub);
		host.SetCurrentTime(NextDay);

		await RunAsync(feedId);

		await Assert.That(host.HttpStub.Requests($"{TestUrls.TmdbApi}/list/{TmdbList}")).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(0);
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MetadataPath)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_ForAStoredTitleMissingItsPosterWhileTmdbHasNotAnswered_AsksTmdbOnce()
	{
		await ConnectTmdbAndPlexAsync();
		host.Movie(TestMedia.FightClub).ServedByTmdb(title: "Fight Club");
		var fightClub = await StoreAsync(TestMedia.FightClub, Complete("Fight Club") with { PosterUrl = null }, RemoteService.Plex);
		var feedId = await SeedTmdbListAsync(TestMedia.FightClub);

		await RunAsync(feedId);
		host.SetCurrentTime(NextDay);
		await RunAsync(feedId);

		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(1);
		await Assert.That((await StoredAsync(fightClub)).Answers.Select(answer => (answer.Provider, answer.Failures))).Contains((RemoteService.Tmdb, 0));
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MetadataPath)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_AfterConnectingANewRatingService_AsksItOncePerTitleBatched()
	{
		await ConnectTmdbAndPlexAsync();
		var fightClub = await StoreAsync(TestMedia.FightClub, Complete("Fight Club"), RemoteService.Tmdb, RemoteService.Plex);
		var inception = await StoreAsync(TestMedia.Inception, Complete("Inception"), RemoteService.Tmdb, RemoteService.Plex);
		var feedId = await SeedTmdbListAsync(TestMedia.FightClub, TestMedia.Inception);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		host.HttpStub.RespondWithJsonForUrl(MdblistTitles, MdblistRatings(("550", 67), ("27205", 74)));

		await RunAsync(feedId);
		host.SetCurrentTime(NextDay);
		await RunAsync(feedId);

		await Assert.That(host.HttpStub.Requests(MdblistTitles)).IsEqualTo(1);
		await Assert.That((await StoredAsync(fightClub)).Details.Ratings.Of(RatingSource.Metacritic)).IsSome();
		await Assert.That((await StoredAsync(inception)).Details.Ratings.Of(RatingSource.Metacritic)).IsSome();
		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_WhenEveryServiceAnsweredWithoutAMetacriticScore_AsksNothing()
	{
		await ConnectTmdbAndPlexAsync();
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		await StoreAsync(TestMedia.FightClub, Complete("Fight Club"), RemoteService.Tmdb, RemoteService.Plex, RemoteService.Mdblist);
		var feedId = await SeedTmdbListAsync(TestMedia.FightClub);

		await RunAsync(feedId);

		await Assert.That(host.HttpStub.Requests(MdblistTitles)).IsEqualTo(0);
		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(0);
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MetadataPath)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_ForARatingFilterOnASourceANewServiceFills_AsksOnlyThatService()
	{
		await ConnectTmdbAndPlexAsync();
		await StoreAsync(TestMedia.FightClub, Complete("Fight Club") with { Genres = [] }, RemoteService.Tmdb);
		var feedId = await SeedTmdbListAsync([TestMedia.FightClub], RatedAbove(5));
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		host.HttpStub.RespondWithJsonForUrl(MdblistTitles, MdblistRatings(("550", 67)));
		host.SetCurrentTime(NextDay);

		await RunAsync(feedId);

		await Assert.That(host.HttpStub.Requests(MdblistTitles)).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(0);
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MetadataPath)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_WhenTheOnlyServiceForAPosterIsBlockedLonger_SettlesWithoutWaitingAndRecordsNoAnswer()
	{
		await ConnectTmdbAndPlexAsync();
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		host.Movie(TestMedia.FightClub).ServedByTmdb(title: "Fight Club");
		var fightClub = await StoreAsync(TestMedia.FightClub, Complete("Fight Club") with { PosterUrl = null }, RemoteService.Plex, RemoteService.Mdblist);
		var feedId = await SeedFeedAsync("cached-list");
		host.MdblistFeedContains("cached-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		host.Resolve<TmdbOptions>().Http.EnrichWaitSeconds = 15;
		await host.RateLimitedAsync(RemoteService.Tmdb, TimeSpan.FromSeconds(10));

		var result = await RunAsync(feedId);

		await Assert.That(result).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That(host.HttpStub.Requests(TmdbDetail(TestMedia.FightClub))).IsEqualTo(0);
		await Assert.That((await StoredAsync(fightClub)).Answers.Select(answer => answer.Provider)).DoesNotContain(RemoteService.Tmdb);
	}

	[Test]
	public async Task RunAsync_ForAFullyCachedList_SendsOnlyItsPage()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		await StoreAsync(TestMedia.FightClub, Complete("Fight Club"), RemoteService.Mdblist);
		await StoreAsync(TestMedia.Inception, Complete("Inception"), RemoteService.Mdblist);
		var feedId = await SeedFeedAsync("cached-list");
		host.MdblistFeedContains("cached-list", Item("Fight Club", MediaType.Movie, Tmdb("550")), Item("Inception", MediaType.Movie, Tmdb("27205")));
		await RunAsync(feedId);
		host.SetCurrentTime(NextDay);
		var before = host.HttpStub.Requests("");

		await RunAsync(feedId);

		await Assert.That(host.HttpStub.Requests("") - before).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(MdblistTitles)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_AtTheFeedReserve_SendsNoTitleRequestAndRecordsNoAnswer()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var fightClub = await StoreAsync(TestMedia.FightClub, Complete("Fight Club"));
		var feedId = await SeedFeedAsync("cached-list");
		host.MdblistFeedContains("cached-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		host.Resolve<MdblistBudget>().Record(SammlarrTestHost.MdblistApiKey, Spent(Now.AddDays(1)));

		await RunAsync(feedId);

		await Assert.That(host.HttpStub.Requests(MdblistTitles)).IsEqualTo(0);
		await Assert.That((await StoredAsync(fightClub)).Answers.Select(answer => answer.Provider)).DoesNotContain(RemoteService.Mdblist);
	}

	[Test]
	public async Task RunAsync_WhenAFilteredFieldsServiceIsBlocked_Waits()
	{
		var time = host.UseFakeTime();
		await ConnectTmdbAndPlexAsync();
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		host.Resolve<TmdbOptions>().Http.EnrichWaitSeconds = 15;
		host.Resolve<TmdbOptions>().Http.MaxWaitSeconds = 15;
		host.Movie(TestMedia.FightClub).ServedByTmdb(title: "Fight Club", genres: ["Drama"]);
		var fightClub = await StoreAsync(TestMedia.FightClub, Complete("Fight Club") with { Genres = [] }, RemoteService.Plex, RemoteService.Mdblist);
		var feedId = await SeedFeedAsync("cached-list", GenreIs("Drama"));
		host.MdblistFeedContains("cached-list", Item("Fight Club", MediaType.Movie, Tmdb("550")));
		await host.RateLimitedAsync(RemoteService.Tmdb, TimeSpan.FromSeconds(10));

		var running = RunAsync(feedId);
		await EventuallyAsync(() => host.Resolve<Lanes>().Gate(new LaneName(RemoteService.Tmdb, Lanes.Rest), new None()).Load(LaneClass.Title).Waiting == 1);
		time.Advance(TimeSpan.FromSeconds(10));

		await Assert.That(await running.WaitAsync(Patience)).IsOfUnionType<FeedRunResult, FeedRunCompleted>();
		await Assert.That((await StoredAsync(fightClub)).Details.Genres).Contains("Drama");
	}

	private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

	private const string MdblistTitles = $"{TestUrls.MdblistApi}/tmdb/movie";

	private static System.Net.Http.Headers.HttpResponseHeaders Spent(DateTimeOffset reset)
	{
		var answered = new HttpResponseMessage(HttpStatusCode.OK);
		answered.Headers.Add("x-ratelimit-remaining", "10");
		answered.Headers.Add("x-ratelimit-reset", $"{reset.ToUnixTimeSeconds()}");
		return answered.Headers;
	}

	private static MovieDetails Complete(string title)
	{
		return new MovieDetails
		{
			Title = title,
			Tagline = "Tagline",
			Summary = "An overview",
			PosterUrl = "/poster.jpg",
			BackdropUrl = "/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 15),
			Genres = ["Drama"],
			Runtime = TimeSpan.FromMinutes(120),
			Ratings = MediaRatings.From([
				new TmdbRating(8f, Some.Of(500)),
				new ImdbRating(8.8f, Some.Of(2_000_000)),
				new RottenTomatoesCriticsRating(79),
				new RottenTomatoesAudienceRating(96),
			]),
		};
	}

	private async Task ConnectTmdbAndPlexAsync()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.WithTmdbAccount(SammlarrTestHost.TmdbApiKey);
		await host.WithPlexAccount(SammlarrTestHost.PlexToken);
	}

	private async Task<MediaId> StoreAsync(MediaSpec spec, MediaDetails details, params RemoteService[] answeredBy)
	{
		var id = await host.Movie(spec).SeedAsync();
		ValueSet<ProviderAnswer> answers = [.. answeredBy.Select(remote => ProviderAnswer.After(remote, host.Enricher(remote).Fills, details, Now))];
		await host.Resolve<IMediaRepository>().SaveDetailsAsync([new DetailsChange(id, new None(), new CachedDetails(details, Now, Now, answers))]);
		return id;
	}

	private async Task<CachedDetails> StoredAsync(MediaId id)
	{
		var stored = (await host.Resolve<IMediaRepository>().GetAsync([id])).Single();
		var cached = await Assert.That(stored.Cached).IsSome();
		return cached!;
	}

	private async Task<FeedRunResult> RunAsync(FeedId feedId) => await host.Resolve<IFeedRunEngine>().RunAsync(feedId, new FeedRunTrigger(new Manual()));

	private Task<FeedId> SeedTmdbListAsync(params MediaSpec[] titles) => SeedTmdbListAsync(titles, null);

	private async Task<FeedId> SeedTmdbListAsync(IReadOnlyList<MediaSpec> titles, QueryFilter? filter)
	{
		var items = titles.Select(title => $$"""{"id":{{title.Ids[0].Value}},"media_type":"movie","title":"{{title.Title}}","release_date":"2024-01-15"}""");
		host.HttpStub.RespondWithJsonForUrl($"{TestUrls.TmdbApi}/list/{TmdbList}", $$"""{"page":1,"total_pages":1,"items":[{{string.Join(",", items)}}]}""");
		var entity = new FeedRow
		{
			Name = "Best of 2024",
			IsEnabled = true,
			Provider = FeedProvider.Tmdb,
			Strategy = new FeedStrategy(new Append()),
			Identifier = TmdbList,
			Filter = filter,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}

	private static QueryFilter RatedAbove(int rating)
	{
		return Filtering(
			new FilterPredicate
			{
				Field = nameof(Contracts.Processing.MediaQueryRow.Rating),
				Operator = FilterPredicateOperator.GreaterThan,
				ValueKind = FilterValueKind.Number,
				Value = $"{rating}",
			}
		);
	}

	private static QueryFilter GenreIs(string genre)
	{
		return Filtering(
			new FilterPredicate
			{
				Field = nameof(Contracts.Processing.MediaQueryRow.Genres),
				Operator = FilterPredicateOperator.Contains,
				Value = $"\"{genre}\"",
			}
		);
	}

	private static QueryFilter Filtering(FilterPredicate predicate)
	{
		return new QueryFilter { Operator = FilterOperator.And, Groups = [new FilterGroup { Operator = FilterOperator.And, Predicates = [predicate] }] };
	}

	private static async Task EventuallyAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(Patience);
		while (!condition())
		{
			await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
		}
	}

	private static string MdblistRatings(params (string Tmdb, int Metacritic)[] titles)
	{
		var entries = titles.Select(title =>
			$$"""{"ids":{"tmdb":{{title.Tmdb}},"mdblist":"m{{title.Tmdb}}"},"ratings":[{"source":"metacritic","value":{{title.Metacritic}},"score":{{title.Metacritic}},"votes":30}]}"""
		);
		return $"[{string.Join(",", entries)}]";
	}

	private static string TmdbDetail(MediaSpec spec) => string.Format(TmdbEndpoints.Detail, TmdbEndpoints.MovieType, spec.Ids[0].Value);

	private static FeedItemSpec Item(string title, MediaType type, params ProviderId[] ids)
	{
		return new FeedItemSpec(title, type, null, ids.ToDictionary(id => id.Provider, id => id.Value));
	}

	private async Task<FeedId> SeedFeedAsync(string identifier, QueryFilter? filter = null)
	{
		var entity = new FeedRow
		{
			Name = "Comedy",
			IsEnabled = true,
			Provider = FeedProvider.Mdblist,
			Strategy = new FeedStrategy(new Append()),
			Identifier = identifier,
			Filter = filter,
		};

		host.Context.Feeds.Add(entity);
		await host.Context.SaveChangesAsync();
		host.Context.ChangeTracker.Clear();
		return FeedId.From(entity.Id);
	}
}
