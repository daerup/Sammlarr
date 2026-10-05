using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Providers;
using Contracts.Settings;
using Core.Media;
using Domain.Auth;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Media;

public class DetailsFetcherTests
{
	private static readonly MediaId Id = MediaId.New();
	private static readonly PlexId Plex = PlexId.From("plex-rating", MediaType.Movie);
	private static readonly TmdbId Tmdb = TmdbId.From("500", MediaType.Movie);
	private static readonly MediaIdentity Identity = new(Id, [Plex, Tmdb]);
	private static readonly MediaIdentity PlexOnly = new(Id, [Plex]);
	private static readonly IReadOnlySet<MediaProvider> Everyone = new HashSet<MediaProvider> { MediaProvider.Tmdb, MediaProvider.Plex };
	private static readonly IReadOnlySet<MediaProvider> OnlyPlex = new HashSet<MediaProvider> { MediaProvider.Plex };

	private static readonly MediaFields Everything = ~MediaField.None | MediaFields.AllRatings;
	private static readonly MediaRatings TmdbOnly = MediaRatings.From([new TmdbRating(8f, Some.Of(500))]);
	private static readonly MediaRatings EverySource = MediaRatings.From([
		new TmdbRating(8f, Some.Of(500)),
		new ImdbRating(8.8f, Some.Of(2_500_000)),
		new RottenTomatoesCriticsRating(87),
		new RottenTomatoesAudienceRating(91),
	]);
	private static readonly Error Unavailable = Error.From(new ProviderUnavailableException(MediaProvider.Tmdb));
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset LastRefresh = Now.AddDays(-3);
	private static readonly ProviderAnswer PlexLeftTheRatings = new(MediaProvider.Plex, MediaFields.AllRatings);
	private static readonly CachedDetails UnratedByPlex = new(Unrated(), LastRefresh, LastRefresh, [PlexLeftTheRatings]);

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatRatesHasNoRating_AsksItOnceAndStoresThatItLeftTheRatingsUnfilled()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails()));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(buffer.NewDetails[Id].Answers).Contains(new ProviderAnswer(MediaProvider.Tmdb, MediaFields.AllRatings));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherFails_StoresNoAnswerForIt()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Missing).IsEqualTo(MediaFields.AllRatings);
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([MediaProvider.Plex]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThrows_KeepsWhatThePrimaryFoundAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Throws(new HttpRequestException("TMDB is down"));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details).IsEqualTo(Complete() with { Tagline = null });
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([MediaProvider.Plex]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherCannotFillAnyMissingField_NeverCallsIt()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Poster);
		deps.Primary(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherCanFillAMissingField_CallsItAndMergesWhatItFound()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Tagline);
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Tagline = "Your mind is the scene of the crime." }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Tagline).IsEqualTo("Your mind is the scene of the crime.");
		await Assert.That(buffer.NewDetails[Id].Answers).Contains(new ProviderAnswer(MediaProvider.Tmdb, MediaFields.None));
	}

	[Test]
	public async Task FetchAsync_WhenOnlyTheRatingIsMissing_TakesItFromAnEnricherThatRates()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Ratings).IsEqualTo(TmdbOnly);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForACompletePrimaryResult_AsksNoEnricherAndStoresOnlyThePrimarysAnswer()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		await Assert.That(buffer.NewDetails[Id].Answers).IsEquivalentTo([new ProviderAnswer(MediaProvider.Plex, MediaFields.None)]);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyTheRatingIsMissing_NeverAsksAnEnricherThatCannotRate()
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.None);
		deps.Primary(Unrated());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenAGapSurvivesEveryEnricher_StoresItAsUnfilledByEach()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails()));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert
			.That(buffer.NewDetails[Id].Answers)
			.IsEquivalentTo([new ProviderAnswer(MediaProvider.Plex, MediaField.Tagline), new ProviderAnswer(MediaProvider.Tmdb, MediaField.Tagline)]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherAnswersAnotherType_IgnoresTheAnswerAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Success.Of(new FoundMedia([TvdbId.From("81189", MediaType.Show)], new ShowDetails { Tagline = "Say my name." })));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Identity).IsEqualTo(Identity);
		await Assert.That(fetched.Details).IsEqualTo(Complete() with { Tagline = null });
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([MediaProvider.Plex]);
	}

	[Test]
	public async Task FetchAsync_WhenTheTitleHasNoIdForTheEnricher_NeverAsksIt()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null }, PlexOnly);
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), Everyone, buffer));

		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([MediaProvider.Plex]);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheUserHasNotConnectedTheEnricher_NeverAsksIt()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), OnlyPlex, buffer));

		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([MediaProvider.Plex]);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatRatesIsRateLimitedAndTheRatingIsMissing_StillAsksItOnce()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(provider => new ProviderLoad(Optional.When(provider is MediaProvider.Tmdb, () => Now.AddMinutes(1)), 0))
		);
		deps.Primary(Unrated() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyOtherSourcesRatingsAreMissing_AsksAnEnricherAndKeepsBothRatings()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Ratings = TmdbOnly });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Answer(new MovieDetails { Ratings = MediaRatings.From([new ImdbRating(8.8f, Some.Of(2_500_000))]) }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Ratings.All.Select(rating => rating.Source)).IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb]);
	}

	[Test]
	public async Task FetchAsync_WhenThePrimaryCopiesTmdbsRatingWithoutVotes_AsksTmdbAndKeepsItsOwnRating()
	{
		var deps = Dependencies.Create(tmdbFills: MediaFields.Rated([RatingSource.Tmdb]));
		deps.Primary(Complete() with { Ratings = MediaRatings.From([new TmdbRating(8.9f, new None()), .. EverySource.All]) });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Ratings.Of(RatingSource.Tmdb).Map(rating => rating.Votes)).IsSome().And.IsEqualTo(500);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheTitleHasARatingAndTheEnricherIsRateLimited_SkipsItForTheOtherSources()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(provider => new ProviderLoad(Optional.When(provider is MediaProvider.Tmdb, () => Now.AddMinutes(1)), 0))
		);
		deps.Primary(Complete() with { Ratings = TmdbOnly });
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherIsRateLimited_SkipsItAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(provider => new ProviderLoad(Optional.When(provider is MediaProvider.Tmdb, () => Now.AddMinutes(1)), 0))
		);
		deps.Primary(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([MediaProvider.Plex]);
	}

	[Test]
	public async Task FetchAsync_WhenAnEnricherAnswersACompleteRecord_KeepsTheTitlesMediaIdAndAddsTheIdsItFound()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia([Tmdb, imdb], Complete())));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Identity.Id).IsEqualTo(Id);
		await Assert.That(fetched.Identity.Externals).IsEquivalentTo(new IExternalMediaId[] { Plex, Tmdb, imdb });
	}

	[Test]
	public async Task FetchAsync_WhenThePrimaryAnswersAnotherType_FailsAndAsksNoEnricher()
	{
		var deps = Dependencies.Create();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Success.Of(new FoundMedia([PlexId.From("plex-rating", MediaType.Show)], new ShowDetails { Title = "Inception" })));
		await using var buffer = deps.Buffer();

		var fetched = await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		await Assert.That(fetched).IsError();
		await Assert.That(buffer.NewDetails).IsEmpty();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenRefreshing_StampsBothTimesWithTheTimeOfTheRefresh()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		await Assert.That(buffer.NewDetails[Id].RefreshedAt).IsEqualTo(Now);
		await Assert.That(buffer.NewDetails[Id].AskedAt).IsEqualTo(Now);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopy_RefreshesItFromThePrimary()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete());
		var stale = new CachedDetails(
			Complete() with
			{
				Title = "Old",
			},
			Now.AddDays(-14),
			Now.AddDays(-14),
			[new ProviderAnswer(MediaProvider.Plex, MediaFields.None)]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Everyone, buffer));

		await Assert.That(fetched.Details.Title).IsEqualTo("Inception");
		await Assert.That(buffer.NewDetails[Id].RefreshedAt).IsEqualTo(Now);
		deps.Search.LookUpAsync(Plex, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForACopyThatIsNotStale_AsksOnlyTheEnrichersThatHaveNotAnsweredAndKeepsWhatIsStored()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Title = "Other", Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Everyone, buffer));

		await Assert.That(fetched.Details).IsEqualTo(Unrated() with { Ratings = TmdbOnly });
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenFillingGaps_KeepsTheRefreshTimeMovesTheAskTimeAndAddsTheNewAnswer()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Everyone, buffer));

		var filled = buffer.NewDetails[Id];
		await Assert.That(filled.RefreshedAt).IsEqualTo(LastRefresh);
		await Assert.That(filled.AskedAt).IsEqualTo(Now);
		await Assert
			.That(filled.Answers)
			.IsEquivalentTo([
				PlexLeftTheRatings,
				new ProviderAnswer(MediaProvider.Tmdb, MediaFields.Rated([RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience])),
			]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherOfAGapFillFails_StillMovesTheAskTimeAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Everyone, buffer));

		var filled = buffer.NewDetails[Id];
		await Assert.That(filled.RefreshedAt).IsEqualTo(LastRefresh);
		await Assert.That(filled.AskedAt).IsEqualTo(Now);
		await Assert.That(filled.Answered).IsEquivalentTo([MediaProvider.Plex]);
	}

	[Test]
	[Arguments(SearchProvider.Tmdb)]
	[Arguments(SearchProvider.Arr)]
	public async Task FetchAsync_WhenSearchIsNotPlex_LooksTheTitleUpOnTmdbByItsTmdbId(SearchProvider search)
	{
		var deps = Dependencies.Create(search: search);
		deps.TmdbSearch.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia(Identity.Externals, Complete())));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		deps.TmdbSearch.LookUpAsync(Tmdb, Any()).WasCalled(Times.Once);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenSearchIsTmdbAndTheTitleIsOnlyKnownToPlex_LooksItUpOnPlex()
	{
		var deps = Dependencies.Create(search: SearchProvider.Tmdb);
		deps.Primary(Complete(), PlexOnly);
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), Everyone, buffer));

		deps.Search.LookUpAsync(Plex, Any()).WasCalled(Times.Once);
		deps.TmdbSearch.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTmdbHasNoRatingAndPlexIsRateLimited_LeavesPlexAlone()
	{
		var deps = Dependencies.Create(
			search: SearchProvider.Tmdb,
			capacity: TestCapacity.With(provider => new ProviderLoad(Optional.When(provider is MediaProvider.Plex, () => Now.AddMinutes(1)), 0))
		);
		deps.TmdbSearch.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia(Identity.Externals, Unrated())));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Missing).IsEqualTo(MediaFields.AllRatings);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FindReusable_WhenAnEnricherTheUserHasConnectedCanFillAGap_IsNone()
	{
		var deps = Dependencies.Create();
		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(UnratedByPlex), Everyone)).IsNone();
	}

	[Test]
	public async Task FindReusable_WhenTheOnlyEnricherThatCanFillAGapIsNotConnected_IsTheStoredCopy()
	{
		var deps = Dependencies.Create();
		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(UnratedByPlex), OnlyPlex)).IsSome().And.IsEqualTo(UnratedByPlex);
	}

	[Test]
	public async Task ConnectedAsync_ReadsTheEnrichersWhoseKeyOrSignInIsReady()
	{
		var deps = Dependencies.Create();
		deps.Keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Error.From("no TMDB key"));
		deps.Credentials.GetAsync(AuthProvider.Plex, Any()).Returns(Success.Of(ICredential.Mock().Object));

		await Assert.That(await deps.Testee.ConnectedAsync(CancellationToken.None)).IsEquivalentTo([MediaProvider.Plex]);
	}

	[Test]
	public async Task ConnectedAsync_WhenReadingASecretThrows_LeavesThatEnricherOut()
	{
		var deps = Dependencies.Create();
		deps.Keys.GetAsync(ApiKeyProvider.Tmdb, Any()).Returns(Success.Of(IApiKey.Mock().Object));
		deps.Credentials.GetAsync(AuthProvider.Plex, Any()).Throws(new HttpRequestException("plex.tv is down"));

		await Assert.That(await deps.Testee.ConnectedAsync(CancellationToken.None)).IsEquivalentTo([MediaProvider.Tmdb]);
	}

	private static async Task<MatchedMedia> Matched(Result<MatchedMedia> result)
	{
		var matched = await Assert.That(result).IsSuccess();
		return matched!;
	}

	private static Result<FoundMedia> Answer(MediaDetails details)
	{
		return Success.Of(new FoundMedia([Tmdb], details));
	}

	private static MovieDetails Complete()
	{
		return new()
		{
			Title = "Inception",
			Tagline = "A dream within a dream.",
			Summary = "A thief steals secrets through dreams.",
			PosterUrl = "poster",
			BackdropUrl = "backdrop",
			ReleaseDate = new DateOnly(2010, 7, 16),
			Genres = ["Science Fiction"],
			Ratings = EverySource,
			Runtime = TimeSpan.FromMinutes(148),
		};
	}

	private static MovieDetails Unrated() => Complete() with { Ratings = MediaRatings.None };

	private sealed record Dependencies(
		Mock<IMediaRepository> MediaRepository,
		Mock<IMediaSearchProvider> Search,
		Mock<IMediaSearchProvider> TmdbSearch,
		Mock<IDetailsEnricher> Plex,
		Mock<IDetailsEnricher> Tmdb,
		Mock<IApiKeyService> Keys,
		Mock<ICredentialService> Credentials,
		DetailsFetcher Testee
	)
	{
		public static Dependencies Create(MediaFields? tmdbFills = null, IProviderCapacity? capacity = null, SearchProvider search = SearchProvider.Plex)
		{
			var mediaRepository = IMediaRepository.Mock();
			mediaRepository.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
			var plexSearch = IMediaSearchProvider.Mock();
			var tmdbSearch = IMediaSearchProvider.Mock();
			var plex = IDetailsEnricher.Mock();
			plex.Fills.Returns(Everything);
			var tmdb = IDetailsEnricher.Mock();
			tmdb.Fills.Returns(tmdbFills ?? Everything);
			var keys = IApiKeyService.Mock();
			var credentials = ICredentialService.Mock();

			var config = IProviderConfigService.Mock();
			config
				.GetAsync(Any())
				.Returns(
					new ProviderConfig
					{
						Sink = SinkKind.Plex,
						TrendingProvider = TrendingProvider.Tmdb,
						SearchProvider = search,
						AuthProvider = AuthProvider.Plex,
						RegistrationMode = RegistrationMode.Open,
					}
				);

			var testee = new DetailsFetcher(
				new FakeKeyedServiceIndex<SearchProvider, IMediaSearchProvider>()
					.Register(SearchProvider.Plex, plexSearch.Object)
					.Register(SearchProvider.Tmdb, tmdbSearch.Object),
				new FakeKeyedServiceIndex<MediaProvider, IDetailsEnricher>().Register(MediaProvider.Plex, plex.Object).Register(MediaProvider.Tmdb, tmdb.Object),
				config.Object,
				capacity ?? TestCapacity.Open(),
				keys.Object,
				credentials.Object,
				Clock(),
				NullLogger<DetailsFetcher>.Instance
			);
			return new Dependencies(mediaRepository, plexSearch, tmdbSearch, plex, tmdb, keys, credentials, testee);
		}

		public void Primary(MovieDetails details, MediaIdentity? identity = null)
		{
			Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia((identity ?? Identity).Externals, details)));
		}

		private static IDateTimeProvider Clock()
		{
			var clock = IDateTimeProvider.Mock();
			clock.Now.Returns(Now);
			return clock.Object;
		}

		public MediaWriteBuffer Buffer() => new(MediaRepository.Object, NullLogger<MediaWriteBuffer>.Instance);
	}
}
