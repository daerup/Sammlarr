using Contracts;
using Contracts.Media;
using Contracts.Media.Artwork;
using Contracts.Providers;
using Contracts.Settings;
using Core.DI;
using Core.Media;
using Core.Tests.Feed;
using Domain.Feed.Trace;
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
	private static readonly IReadOnlySet<RemoteService> Everyone = new HashSet<RemoteService> { RemoteService.Tmdb, RemoteService.Plex };
	private static readonly IReadOnlySet<RemoteService> OnlyPlex = new HashSet<RemoteService> { RemoteService.Plex };

	private static readonly MediaFields Everything = ~MediaField.None | MediaFields.Rated(RemoteService.Plex.SuppliedRatingSources());
	private static readonly MediaRatings TmdbOnly = MediaRatings.From([new TmdbRating(8f, Some.Of(500))]);
	private static readonly MediaRatings EverySource = MediaRatings.From([
		new TmdbRating(8f, Some.Of(500)),
		new ImdbRating(8.8f, Some.Of(2_500_000)),
		new RottenTomatoesCriticsRating(87),
		new RottenTomatoesAudienceRating(91),
	]);
	private static readonly Error Unavailable = Error.From(new RemoteUnavailableException(RemoteService.Tmdb));
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset LastRefresh = Now.AddDays(-3);
	private static readonly CancellationToken WaitingThrows = new(canceled: true);
	private static readonly ProviderAnswer PlexLeftTheRatings = new(RemoteService.Plex, MediaFields.AllRatings, LastRefresh);
	private static readonly CachedDetails UnratedByPlex = new(Unrated(), LastRefresh, LastRefresh, [PlexLeftTheRatings]);
	private const string TmdbImages = "https://image.tmdb.org/t/p/original/inception.jpg";
	private const string OtherImages = "https://static.tvmaze.com/uploads/images/original_untouched/inception.jpg";

	[Test]
	public async Task FetchAsync_WhenAnEnricherDefersTheTitle_KeepsTheStoredTitleAndTakesTheRest()
	{
		var deps = Dependencies.Create();
		deps.Plex.Defers.Returns(MediaField.Title);
		deps.PlexAnswers(Complete() with { Title = "インセプション" });
		var stored = new CachedDetails(
			Complete() with
			{
				Tagline = null,
			},
			LastRefresh,
			LastRefresh,
			[new ProviderAnswer(RemoteService.Tmdb, MediaField.Tagline, LastRefresh)]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stored), Everyone, buffer));

		await Assert.That(fetched.Details.Title).IsEqualTo("Inception");
		await Assert.That(fetched.Details.Tagline).IsEqualTo(Complete().Tagline);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyAnEnricherThatDefersTheTitleAnswers_FillsTheEmptyTitleWithIt()
	{
		var deps = Dependencies.Create();
		deps.Plex.Defers.Returns(MediaField.Title);
		deps.PlexAnswers(Complete() with { Title = "インセプション" });
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), OnlyPlex, buffer));

		await Assert.That(fetched.Details.Title).IsEqualTo("インセプション");
	}

	[Test]
	public async Task FetchAsync_WhenAnAnswerBringsAPosterNoResizerSizes_KeepsTheStoredResizablePoster()
	{
		var deps = Dependencies.Create(resizer: TestArtwork.Resizing(url => url.StartsWith(TmdbImages, StringComparison.Ordinal)));
		deps.PlexAnswers(Complete() with { PosterUrl = OtherImages });
		var stored = new CachedDetails(
			Complete() with
			{
				Tagline = null,
				PosterUrl = TmdbImages,
			},
			LastRefresh,
			LastRefresh,
			[new ProviderAnswer(RemoteService.Tmdb, MediaField.Tagline, LastRefresh)]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stored), Everyone, buffer));

		await Assert.That(fetched.Details.PosterUrl).IsEqualTo(TmdbImages);
		await Assert.That(fetched.Details.Tagline).IsEqualTo(Complete().Tagline);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyAPosterNoResizerSizesArrives_FillsTheEmptyPosterWithIt()
	{
		var deps = Dependencies.Create(resizer: TestArtwork.Resizing(url => url.StartsWith(TmdbImages, StringComparison.Ordinal)));
		deps.PlexAnswers(Complete() with { PosterUrl = OtherImages });
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), OnlyPlex, buffer));

		await Assert.That(fetched.Details.PosterUrl).IsEqualTo(OtherImages);
	}

	[Test]
	public async Task FetchAsync_WhenAResizablePosterArrives_ReplacesAStoredOneNoResizerSizes()
	{
		var deps = Dependencies.Create(resizer: TestArtwork.Resizing(url => url.StartsWith(TmdbImages, StringComparison.Ordinal)));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Complete() with { PosterUrl = TmdbImages }));
		var stored = new CachedDetails(
			Complete() with
			{
				Tagline = null,
				PosterUrl = OtherImages,
			},
			LastRefresh,
			LastRefresh,
			[new ProviderAnswer(RemoteService.Plex, MediaField.Tagline, LastRefresh)]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stored), Everyone, buffer));

		await Assert.That(fetched.Details.PosterUrl).IsEqualTo(TmdbImages);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatRatesHasNoRating_AsksItOnceAndStoresThatItLeftTheRatingsUnfilled()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Unrated()));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert
			.That(buffer.NewDetails[Id].Answers)
			.Contains(new ProviderAnswer(RemoteService.Tmdb, MediaFields.Rated(RemoteService.Plex.SuppliedRatingSources()), Now));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherFails_StoresNoAnswerForIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Missing).IsEqualTo(MediaFields.AllRatings);
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([RemoteService.Plex]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThrows_KeepsWhatPlexFoundAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Throws(new HttpRequestException("TMDB is down"));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details).IsEqualTo(Complete() with { Tagline = null });
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([RemoteService.Plex]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherCannotFillAnyMissingField_NeverCallsIt()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Poster);
		deps.PlexAnswers(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherCanFillAMissingField_CallsItAndMergesWhatItFound()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Tagline);
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Tagline = "Your mind is the scene of the crime." }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Tagline).IsEqualTo("Your mind is the scene of the crime.");
		await Assert.That(buffer.NewDetails[Id].Answers).Contains(new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now));
	}

	[Test]
	public async Task FetchAsync_WhenOnlyTheRatingIsMissing_TakesItFromAnEnricherThatRates()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Ratings).IsEqualTo(TmdbOnly);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheFirstAnswerIsComplete_AsksNoOtherEnricherAndStoresOnlyThatAnswer()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		await Assert.That(buffer.NewDetails[Id].Answers).IsEquivalentTo([new ProviderAnswer(RemoteService.Plex, MediaFields.None, Now)]);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyTheRatingIsMissing_NeverAsksAnEnricherThatCannotRate()
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.None);
		deps.PlexAnswers(Unrated());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenAGapSurvivesEveryEnricher_StoresItAsUnfilledByEach()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Complete() with { Tagline = null }));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert
			.That(buffer.NewDetails[Id].Answers)
			.IsEquivalentTo([new ProviderAnswer(RemoteService.Plex, MediaField.Tagline, Now), new ProviderAnswer(RemoteService.Tmdb, MediaField.Tagline, Now)]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherAnswersAnotherType_IgnoresTheAnswerAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Success.Of(new FoundMedia([TvdbId.From("81189", MediaType.Show)], new ShowDetails { Tagline = "Say my name." })));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Identity).IsEqualTo(Identity);
		await Assert.That(fetched.Details).IsEqualTo(Complete() with { Tagline = null });
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([RemoteService.Plex]);
	}

	[Test]
	public async Task FetchAsync_WhenTheTitleHasNoIdForTheEnricher_NeverAsksIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null }, PlexOnly);
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), Everyone, buffer));

		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([RemoteService.Plex]);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheUserHasNotConnectedTheEnricher_NeverAsksIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), OnlyPlex, buffer));

		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([RemoteService.Plex]);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatFillsAGapIsBlockedWithinItsEnrichWait_WaitsAndAsksIt()
	{
		var deps = Dependencies.Create(capacity: TmdbBlocked(() => Now.AddMilliseconds(20)));
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Tagline = "A dream within a dream." }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Tagline).IsEqualTo("A dream within a dream.");
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheBlockOutlastsTheEnrichWait_SkipsTheEnricherEvenForAMissingRating()
	{
		var deps = Dependencies.Create(capacity: TmdbBlocked(() => Now.AddMinutes(1)));
		deps.PlexAnswers(Unrated());
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer, WaitingThrows));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_DuringARun_RecordsADetailsHop()
	{
		using var trace = new TitleTrace();
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That((await trace.NodeAsync(RemoteService.Plex, TraceStage.Details)).Answered).IsEqualTo(1);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherIsBlocked_RecordsGaveUp()
	{
		using var trace = new TitleTrace();
		var deps = Dependencies.Create(capacity: TmdbBlocked(() => Now.AddMinutes(1)));
		deps.PlexAnswers(Unrated());
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer, WaitingThrows));

		await Assert.That((await trace.NodeAsync(RemoteService.Tmdb, TraceStage.Details)).GaveUp).IsEqualTo(1);
	}

	[Test]
	public async Task FetchAsync_WhileWaitingForABlockedEnricher_HonoursCancellation()
	{
		var deps = Dependencies.Create(capacity: TmdbBlocked(() => Now.AddMilliseconds(20)));
		deps.PlexAnswers(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await Assert.That(async () => await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer, WaitingThrows)).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task FetchAsync_WhenTheBlockIsExtendedWhileWaiting_WaitsForTheEnricherOnlyOnce()
	{
		var pushes = 0;
		var deps = Dependencies.Create(capacity: TmdbBlocked(() => Now.AddMilliseconds(20 + pushes++)));
		deps.PlexAnswers(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(pushes).IsEqualTo(3);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyOtherSourcesRatingsAreMissing_AsksAnEnricherAndKeepsBothRatings()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Ratings = TmdbOnly });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Answer(new MovieDetails { Ratings = MediaRatings.From([new ImdbRating(8.8f, Some.Of(2_500_000))]) }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Ratings.All.Select(rating => rating.Source)).IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb]);
	}

	[Test]
	public async Task FetchAsync_WhenPlexCopiesTmdbsRatingWithoutVotes_AsksTmdbAndKeepsItsOwnRating()
	{
		var deps = Dependencies.Create(tmdbFills: MediaFields.Rated([RatingSource.Tmdb]));
		deps.PlexAnswers(Complete() with { Ratings = MediaRatings.From([new TmdbRating(8.9f, new None()), .. EverySource.All]) });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details.Ratings.Of(RatingSource.Tmdb).Map(rating => rating.Votes)).IsSome().And.IsEqualTo(500);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherIsRateLimited_SkipsItAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(remote => new RemoteLoad(Optional.When(remote is RemoteService.Tmdb, () => Now.AddMinutes(1)), 0))
		);
		deps.PlexAnswers(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([RemoteService.Plex]);
	}

	[Test]
	public async Task FetchAsync_WhenAnEnricherAnswersACompleteRecord_KeepsTheTitlesMediaIdAndAddsTheIdsItFound()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia([Tmdb, imdb], Complete())));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Identity.Id).IsEqualTo(Id);
		await Assert.That(fetched.Identity.Keys).IsEquivalentTo(new IExternalMediaId[] { Plex, Tmdb, imdb });
	}

	[Test]
	public async Task FetchAsync_WhenThePreferredEnricherAnswersAnotherType_TakesTheNextOnesAnswer()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Success.Of(new FoundMedia([PlexId.From("plex-rating", MediaType.Show)], new ShowDetails { Title = "Inception" })));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Complete()));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		await Assert.That(fetched.Details).IsEqualTo(Complete());
		await Assert.That(buffer.NewDetails[Id].Answered).IsEquivalentTo([RemoteService.Tmdb]);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task FetchAsync_WhenNoEnricherAnswersForAnUncachedOrStaleTitle_FailsAndStoresNothing(bool stale)
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		var stored = Optional.When(stale, () => new CachedDetails(Complete(), Now.AddDays(-14), Now.AddDays(-14), [PlexLeftTheRatings]));

		var fetched = await deps.Testee.FetchAsync(Identity, stored, Everyone, buffer);

		await Assert.That(fetched).IsError();
		await Assert.That(buffer.NewDetails).IsEmpty();
	}

	[Test]
	public async Task FetchAsync_WhenEveryEnricherIsBlockedPastItsWait_FailsAsUnavailable()
	{
		var deps = Dependencies.Create(capacity: TestCapacity.With(_ => new RemoteLoad(Some.Of(Now.AddMinutes(1)), 0)));
		await using var buffer = deps.Buffer();

		var error = await Assert.That(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer)).IsError();

		await Assert.That(error!.CausedBy<RemoteUnavailableException>()).IsSome();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenAnEnricherThrows_FailsAsUnavailable()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).Throws(new HttpRequestException("Plex is down"));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("not found"));
		await using var buffer = deps.Buffer();

		var error = await Assert.That(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer)).IsError();

		await Assert.That(error!.CausedBy<RemoteUnavailableException>()).IsSome();
	}

	[Test]
	public async Task FetchAsync_WhenNoEnricherKnowsTheTitle_FailsWithoutBlamingAProvider()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("not found"));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("not found"));
		await using var buffer = deps.Buffer();

		var error = await Assert.That(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer)).IsError();

		await Assert.That(error!.CausedBy<RemoteUnavailableException>()).IsNone();
	}

	[Test]
	public async Task FetchAsync_ForATitleKnownOnlyByItsImdbId_AsksTmdbByThatId()
	{
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia([Tmdb, imdb], Complete())));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(new MediaIdentity(Id, [imdb]), new None(), Everyone, buffer));

		await Assert.That(fetched.Details).IsEqualTo(Complete());
		deps.Tmdb.LookUpAsync(imdb, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForATitleWithATvdbAndAnImdbId_AsksTmdbByTheTvdbId()
	{
		var tvdb = TvdbId.From("81189", MediaType.Movie);
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Complete()));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(new MediaIdentity(Id, [ImdbId.From("tt1375666", MediaType.Movie), tvdb]), new None(), Everyone, buffer));

		deps.Tmdb.LookUpAsync(tvdb, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForAGapInATitleKnownOnlyByItsImdbId_AsksTmdbByThatId()
	{
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(new MediaIdentity(Id, [imdb]), Some.Of(UnratedByPlex), Everyone, buffer));

		await Assert.That(fetched.Details.Ratings).IsEqualTo(TmdbOnly);
		deps.Tmdb.LookUpAsync(imdb, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenRefreshing_StampsBothTimesWithTheTimeOfTheRefresh()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer);

		await Assert.That(buffer.NewDetails[Id].RefreshedAt).IsEqualTo(Now);
		await Assert.That(buffer.NewDetails[Id].AskedAt).IsEqualTo(Now);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopy_RefreshesItFromThePreferredEnricher()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		var stale = new CachedDetails(
			Complete() with
			{
				Title = "Old",
			},
			Now.AddDays(-14),
			Now.AddDays(-14),
			[new ProviderAnswer(RemoteService.Plex, MediaFields.None, Now.AddDays(-14))]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Everyone, buffer));

		await Assert.That(fetched.Details.Title).IsEqualTo("Inception");
		await Assert.That(buffer.NewDetails[Id].RefreshedAt).IsEqualTo(Now);
		deps.Plex.LookUpAsync(Plex, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForACopyThatIsNotStale_AsksOnlyTheEnrichersThatHaveNotAnsweredAndPutsTheirAnswersOverTheStoredCopy()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Title = "Other", Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Everyone, buffer));

		await Assert.That(fetched.Details).IsEqualTo(Unrated() with { Title = "Other", Ratings = TmdbOnly });
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenFillingGaps_KeepsTheRefreshTimeMovesTheAskTimeAndAddsTheNewAnswer()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Unrated() with { Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Everyone, buffer));

		var filled = buffer.NewDetails[Id];
		await Assert.That(filled.RefreshedAt).IsEqualTo(LastRefresh);
		await Assert.That(filled.AskedAt).IsEqualTo(Now);
		await Assert
			.That(filled.Answers)
			.IsEquivalentTo([
				PlexLeftTheRatings,
				new ProviderAnswer(
					RemoteService.Tmdb,
					MediaFields.Rated([RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience]),
					Now
				),
			]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherOfAGapFillFails_KeepsTheStoredCopyMovesTheAskTimeAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Everyone, buffer));

		var filled = buffer.NewDetails[Id];
		await Assert.That(filled.RefreshedAt).IsEqualTo(LastRefresh);
		await Assert.That(filled.AskedAt).IsEqualTo(Now);
		await Assert.That(filled.Answered).IsEquivalentTo([RemoteService.Plex]);
		await Assert.That(filled.Details).IsEqualTo(UnratedByPlex.Details);
	}

	[Test]
	public async Task FetchAsync_WhenFillingGaps_PutsTheSecondAnswerOverTheStoredCopyToo()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null, PosterUrl = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Tagline = "A dream within a dream.", PosterUrl = "tmdb-poster" }));
		var stored = new CachedDetails(Complete() with { Tagline = null, PosterUrl = "stored-poster" }, LastRefresh, LastRefresh, []);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stored), Everyone, buffer));

		await Assert.That(fetched.Details.PosterUrl).IsEqualTo("tmdb-poster");
	}

	[Test]
	public async Task FetchAsync_WhenAnAnswerLacksAFieldTheStoredCopyHas_ListsItAsUnfilledByThatProvider()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Unrated() with { Tagline = null, Ratings = TmdbOnly }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Everyone, buffer));

		await Assert.That(fetched.Details.Tagline).IsEqualTo("A dream within a dream.");
		await Assert.That(buffer.NewDetails[Id].Answers.Single(answer => answer.Provider == RemoteService.Tmdb).Unfilled.Overlaps(MediaField.Tagline)).IsTrue();
	}

	[Test]
	[Arguments(13, "A dream within a dream.")]
	[Arguments(14, null)]
	public async Task FetchAsync_ForAStaleCopy_KeepsAValueTheRefreshLacksOnlyWhileAProviderGaveItWithinTwoWeeks(int daysAgo, string? tagline)
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Complete() with { Tagline = null }));
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-20),
			Now.AddDays(-daysAgo),
			[new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now.AddDays(-daysAgo))]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Everyone, buffer));

		await Assert.That(fetched.Details.Tagline).IsEqualTo(tagline);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopyAnsweredByAnEnricherNoLongerRegistered_KeepsNothingItGave()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Complete() with { Tagline = null }));
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-20),
			Now.AddDays(-1),
			[new ProviderAnswer(RemoteService.Wikidata, MediaFields.None, Now.AddDays(-1))]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Everyone, buffer));

		await Assert.That(fetched.Details.Tagline).IsNull();
	}

	[Test]
	[Arguments(3, true)]
	[Arguments(14, false)]
	public async Task FetchAsync_ForAStaleCopyWhilePlexIsDown_KeepsPlexsRatingsOnlyWhenItGaveThemWithinTwoWeeks(int daysAgo, bool kept)
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.None | MediaFields.Rated([RatingSource.Tmdb]));
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(Unrated() with { Ratings = TmdbOnly }));
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-20),
			Now.AddDays(-daysAgo),
			[
				new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now.AddDays(-20)),
				new ProviderAnswer(RemoteService.Plex, MediaFields.None, Now.AddDays(-daysAgo)),
			]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Everyone, buffer));

		await Assert.That(fetched.Details.Ratings).IsEqualTo(kept ? EverySource : TmdbOnly);
		await Assert.That(buffer.NewDetails[Id].Answered.Contains(RemoteService.Plex)).IsEqualTo(kept);
		await Assert.That(buffer.NewDetails[Id].RefreshedAt).IsEqualTo(Now);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopyOpenedWithoutATmdbKey_DropsWhatOnlyTmdbGave()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-14),
			Now.AddDays(-14),
			[new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now.AddDays(-14)), PlexLeftTheRatings]
		);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), OnlyPlex, buffer));

		await Assert.That(fetched.Details).IsEqualTo(Unrated());
	}

	[Test]
	[Arguments(SearchProvider.Tmdb)]
	[Arguments(SearchProvider.Arr)]
	public async Task FetchAsync_WhenSearchIsNotPlex_AsksTmdbFirstByItsTmdbId(SearchProvider search)
	{
		var deps = Dependencies.Create(search: search);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia([.. Identity.Keys], Complete())));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer));

		deps.Tmdb.LookUpAsync(Tmdb, Any()).WasCalled(Times.Once);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenSearchIsTmdbAndTheTitleIsOnlyKnownToPlex_AsksPlex()
	{
		var deps = Dependencies.Create(search: SearchProvider.Tmdb);
		deps.PlexAnswers(Complete(), PlexOnly);
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), Everyone, buffer));

		deps.Plex.LookUpAsync(Plex, Any()).WasCalled(Times.Once);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenPlexIsBrieflyBlockedButHasNoEnrichWait_AsksOnlyTmdb()
	{
		var deps = Dependencies.Create(
			search: SearchProvider.Tmdb,
			capacity: TestCapacity.With(remote => new RemoteLoad(Optional.When(remote is RemoteService.Plex, () => Now.AddMilliseconds(20)), 0))
		);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia([.. Identity.Keys], Unrated())));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Everyone, buffer, WaitingThrows));

		await Assert.That(fetched.Details.Missing).IsEqualTo(MediaFields.AllRatings);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
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
	public async Task ConnectedAsync_IsWhatReadinessSaysOfTheEnrichers()
	{
		var deps = Dependencies.Create();
		deps.Readiness.ConnectedAsync(Any(), Any()).Returns(new HashSet<RemoteService> { RemoteService.Plex });

		await Assert.That(await deps.Testee.ConnectedAsync(CancellationToken.None)).IsEquivalentTo([RemoteService.Plex]);
		deps.Readiness.ConnectedAsync(
				Is<IEnumerable<RemoteService>>(asked => asked!.Order().SequenceEqual(new[] { RemoteService.Tmdb, RemoteService.Plex })),
				Any()
			)
			.WasCalled(Times.Once);
	}

	private static async Task<MatchedMedia> Matched(Result<MatchedMedia> result)
	{
		var matched = await Assert.That(result).IsSuccess();
		return matched!;
	}

	private static IRemoteCapacity TmdbBlocked(Func<DateTimeOffset> until)
	{
		return TestCapacity.With(remote => new RemoteLoad(Optional.When(remote is RemoteService.Tmdb, until), 0, EnrichWait: TimeSpan.FromSeconds(1)));
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
		Mock<IDetailsEnricher> Plex,
		Mock<IDetailsEnricher> Tmdb,
		Mock<IServiceReadiness> Readiness,
		DetailsFetcher Testee
	)
	{
		public static Dependencies Create(
			MediaFields? tmdbFills = null,
			IRemoteCapacity? capacity = null,
			SearchProvider search = SearchProvider.Plex,
			IArtworkResizer? resizer = null
		)
		{
			var mediaRepository = IMediaRepository.Mock();
			var plex = IDetailsEnricher.Mock();
			plex.Fills.Returns(Everything);
			plex.Accepts.Returns([MediaProvider.Plex]);
			var tmdb = IDetailsEnricher.Mock();
			tmdb.Fills.Returns(tmdbFills ?? Everything);
			tmdb.Accepts.Returns([MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Imdb]);
			var readiness = IServiceReadiness.Mock();

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
				new KeyedServiceSet<RemoteService, IDetailsEnricher>([new(RemoteService.Plex, plex.Object), new(RemoteService.Tmdb, tmdb.Object)]),
				TestClaims.Granting().Object,
				config.Object,
				capacity ?? TestCapacity.Open(),
				readiness.Object,
				[resizer ?? TestArtwork.ResizesAnything()],
				Clock(),
				NullLogger<DetailsFetcher>.Instance
			);
			return new Dependencies(mediaRepository, plex, tmdb, readiness, testee);
		}

		public void PlexAnswers(MovieDetails details, MediaIdentity? identity = null)
		{
			Plex.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia([.. (identity ?? Identity).Keys], details)));
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
