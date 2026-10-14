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
using Shared.Options;
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
	private static readonly Answer<FoundMedia> Down = new Unavailable(RemoteService.Tmdb, new None(), true);
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset LastRefresh = Now.AddDays(-3);
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
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stored), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Title).IsEqualTo("Inception");
		await Assert.That(fetched.Details.Tagline).IsEqualTo(Complete().Tagline);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyAnEnricherThatDefersTheTitleAnswers_FillsTheEmptyTitleWithIt()
	{
		var deps = Dependencies.Create();
		deps.Plex.Defers.Returns(MediaField.Title);
		deps.PlexAnswers(Complete() with { Title = "インセプション" });
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), Run(OnlyPlex, writer)));

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
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stored), Run(Everyone, writer)));

		await Assert.That(fetched.Details.PosterUrl).IsEqualTo(TmdbImages);
		await Assert.That(fetched.Details.Tagline).IsEqualTo(Complete().Tagline);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyAPosterNoResizerSizesArrives_FillsTheEmptyPosterWithIt()
	{
		var deps = Dependencies.Create(resizer: TestArtwork.Resizing(url => url.StartsWith(TmdbImages, StringComparison.Ordinal)));
		deps.PlexAnswers(Complete() with { PosterUrl = OtherImages });
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), Run(OnlyPlex, writer)));

		await Assert.That(fetched.Details.PosterUrl).IsEqualTo(OtherImages);
	}

	[Test]
	public async Task FetchAsync_WhenAResizablePosterArrives_ReplacesAStoredOneNoResizerSizes()
	{
		var deps = Dependencies.Create(resizer: TestArtwork.Resizing(url => url.StartsWith(TmdbImages, StringComparison.Ordinal)));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Complete() with { PosterUrl = TmdbImages }));
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
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stored), Run(Everyone, writer)));

		await Assert.That(fetched.Details.PosterUrl).IsEqualTo(TmdbImages);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatRatesHasNoRating_AsksItOnceAndStoresThatItLeftTheRatingsUnfilled()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Unrated()));
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert
			.That((await deps.WrittenAsync(writer))[Id].Answers)
			.Contains(new ProviderAnswer(RemoteService.Tmdb, MediaFields.Rated(RemoteService.Plex.SuppliedRatingSources()), Now));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherFailsAfterSending_StoresAFailureForIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Missing).IsEqualTo(MediaFields.AllRatings);
		await Assert.That(Outcomes(await deps.WrittenAsync(writer))).IsEquivalentTo([(RemoteService.Plex, 0), (RemoteService.Tmdb, 1)]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherFaults_KeepsWhatPlexFoundAndStoresAFailureForIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any())
			.Returns(new Faulted(RemoteService.Tmdb, Error.From(new InvalidOperationException("unreadable"))));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details).IsEqualTo(Complete() with { Tagline = null });
		await Assert.That(Outcomes(await deps.WrittenAsync(writer))).IsEquivalentTo([(RemoteService.Plex, 0), (RemoteService.Tmdb, 1)]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherCannotFillAnyMissingField_NeverCallsIt()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Poster);
		deps.PlexAnswers(Complete() with { Tagline = null });
		await using var writer = deps.Writer();

		await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherCanFillAMissingField_CallsItAndMergesWhatItFound()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Tagline);
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(new MovieDetails { Tagline = "Your mind is the scene of the crime." }));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Tagline).IsEqualTo("Your mind is the scene of the crime.");
		await Assert.That((await deps.WrittenAsync(writer))[Id].Answers).Contains(new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now));
	}

	[Test]
	public async Task FetchAsync_WhenOnlyTheRatingIsMissing_TakesItFromAnEnricherThatRates()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Ratings).IsEqualTo(TmdbOnly);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheFirstAnswerIsComplete_AsksNoOtherEnricherAndStoresOnlyThatAnswer()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		await using var writer = deps.Writer();

		await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
		await Assert.That((await deps.WrittenAsync(writer))[Id].Answers).IsEquivalentTo([new ProviderAnswer(RemoteService.Plex, MediaFields.None, Now)]);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyTheRatingIsMissing_NeverAsksAnEnricherThatCannotRate()
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.None);
		deps.PlexAnswers(Unrated());
		await using var writer = deps.Writer();

		await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenAGapSurvivesEveryEnricher_StoresItAsUnfilledByEach()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Complete() with { Tagline = null }));
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert
			.That((await deps.WrittenAsync(writer))[Id].Answers)
			.IsEquivalentTo([new ProviderAnswer(RemoteService.Plex, MediaField.Tagline, Now), new ProviderAnswer(RemoteService.Tmdb, MediaField.Tagline, Now)]);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherAnswersAnotherType_IgnoresTheAnswerAndStoresItAsFillingNothing()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any())
			.Returns(new Found<FoundMedia>(new FoundMedia([TvdbId.From("81189", MediaType.Show)], new ShowDetails { Tagline = "Say my name." })));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Identity).IsEqualTo(Identity);
		await Assert.That(fetched.Details).IsEqualTo(Complete() with { Tagline = null });
		await Assert.That((await deps.WrittenAsync(writer))[Id].Answers).Contains(new ProviderAnswer(RemoteService.Tmdb, Everything, Now));
	}

	[Test]
	public async Task FetchAsync_WhenTheTitleHasNoIdForTheEnricher_NeverAsksIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null }, PlexOnly);
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), Run(Everyone, writer)));

		await Assert.That(Outcomes(await deps.WrittenAsync(writer))).IsEquivalentTo([(RemoteService.Plex, 0)]);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheUserHasNotConnectedTheEnricher_NeverAsksIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(OnlyPlex, writer)));

		await Assert.That(Outcomes(await deps.WrittenAsync(writer))).IsEquivalentTo([(RemoteService.Plex, 0)]);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatFillsAGapIsBlockedWithinItsEnrichWait_AsksItAndLeavesTheWaitToItsLane()
	{
		var deps = Dependencies.Create(capacity: TmdbBlocked(() => Now.AddMilliseconds(20)));
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(new MovieDetails { Tagline = "A dream within a dream." }));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Tagline).IsEqualTo("A dream within a dream.");
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheBlockOutlastsTheEnrichWait_SkipsTheEnricherEvenForAMissingRatingAndRecordsGaveUp()
	{
		using var trace = new TitleTrace();
		var deps = Dependencies.Create(capacity: TmdbBlocked(() => Now.AddMinutes(1)));
		deps.PlexAnswers(Unrated());
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
		await Assert.That((await trace.NodeAsync(RemoteService.Tmdb, TraceStage.Details)).GaveUp).IsEqualTo(1);
	}

	[Test]
	public async Task FetchAsync_DuringARun_RecordsADetailsHop()
	{
		using var trace = new TitleTrace();
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That((await trace.NodeAsync(RemoteService.Plex, TraceStage.Details)).Answered).IsEqualTo(1);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyOtherSourcesRatingsAreMissing_AsksAnEnricherAndKeepsBothRatings()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Ratings = TmdbOnly });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any())
			.Returns(Answer(new MovieDetails { Ratings = MediaRatings.From([new ImdbRating(8.8f, Some.Of(2_500_000))]) }));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Ratings.All.Select(rating => rating.Source)).IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb]);
	}

	[Test]
	public async Task FetchAsync_WhenPlexCopiesTmdbsRatingWithoutVotes_AsksTmdbAndKeepsItsOwnRating()
	{
		var deps = Dependencies.Create(tmdbFills: MediaFields.Rated([RatingSource.Tmdb]));
		deps.PlexAnswers(Complete() with { Ratings = MediaRatings.From([new TmdbRating(8.9f, new None()), .. EverySource.All]) });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Ratings.Of(RatingSource.Tmdb).Map(rating => rating.Votes)).IsSome().And.IsEqualTo(500);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherIsRateLimited_SkipsItAndStoresNoAnswerForIt()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(remote => RemoteLoad.Idle with { BlockedUntil = Optional.When(remote is RemoteService.Tmdb, () => Now.AddMinutes(1)) })
		);
		deps.PlexAnswers(Complete() with { Tagline = null });
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
		await Assert.That(Outcomes(await deps.WrittenAsync(writer))).IsEquivalentTo([(RemoteService.Plex, 0)]);
	}

	[Test]
	public async Task FetchAsync_WhenAnEnricherAnswersACompleteRecord_KeepsTheTitlesMediaIdAndAddsTheIdsItFound()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Unrated());
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Found<FoundMedia>(new FoundMedia([Tmdb, imdb], Complete())));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Identity.Id).IsEqualTo(Id);
		await Assert.That(fetched.Identity.Keys).IsEquivalentTo(new IExternalMediaId[] { Plex, Tmdb, imdb });
	}

	[Test]
	public async Task FetchAsync_ForTheSecondEnricher_PassesTheIdsTheFirstOneFound()
	{
		var deps = Dependencies.Create();
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Found<FoundMedia>(new FoundMedia([Plex, Tmdb, imdb], Unrated())));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Is<ValueSet<IIdentityKey>>(keys => keys!.Contains(imdb)), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenThePreferredEnricherAnswersAnotherType_TakesTheNextOnesAnswer()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any())
			.Returns(new Found<FoundMedia>(new FoundMedia([PlexId.From("plex-rating", MediaType.Show)], new ShowDetails { Title = "Inception" })));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Complete()));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details).IsEqualTo(Complete());
		await Assert.That(Outcomes(await deps.WrittenAsync(writer))).IsEquivalentTo([(RemoteService.Plex, 0), (RemoteService.Tmdb, 0)]);
	}

	[Test]
	public async Task FetchAsync_WhenNoEnricherAnswersForAnUncachedTitle_FailsAndStoresNothing()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		await using var writer = deps.Writer();

		var fetched = await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer));

		await Assert.That(fetched).IsError();
		await Assert.That(await deps.WrittenAsync(writer)).IsEmpty();
	}

	[Test]
	public async Task FetchAsync_WhenNoEnricherAnswersForAStaleTitle_FailsAndStoresOnlyTheFailures()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		await using var writer = deps.Writer();
		var stale = new CachedDetails(Complete(), Now.AddDays(-14), Now.AddDays(-14), [PlexLeftTheRatings with { AnsweredAt = Now.AddDays(-14) }]);

		var fetched = await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer));

		await Assert.That(fetched).IsError();
		var written = (await deps.WrittenAsync(writer))[Id];
		await Assert.That(written.Details).IsEqualTo(stale.Details);
		await Assert.That(written.RefreshedAt).IsEqualTo(stale.RefreshedAt);
		await Assert.That(written.Answers.Select(answer => (answer.Provider, answer.Failures))).IsEquivalentTo([(RemoteService.Plex, 1), (RemoteService.Tmdb, 1)]);
	}

	[Test]
	public async Task LoadAsync_WhenALaneRefusesBeforeSending_RecordsNothing()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Unavailable(RemoteService.Tmdb, new None(), false));
		await using var writer = deps.Writer();

		await deps.Testee.LoadAsync(Stored(UnratedByPlex), MediaDetails.Empty(MediaType.Movie), Run(Everyone, writer), CancellationToken.None);

		await Assert.That(await deps.WrittenAsync(writer)).IsEmpty();
	}

	[Test]
	public async Task LoadAsync_WhenTheLaneIsDown_RecordsNothing()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(remote => remote is RemoteService.Tmdb ? RemoteLoad.Idle with { Down = true } : RemoteLoad.Idle)
		);
		deps.PlexAnswers(Unrated());
		var stale = new CachedDetails(Unrated(), Now.AddDays(-14), Now.AddDays(-14), []);
		await using var writer = deps.Writer();

		await deps.Testee.LoadAsync(Stored(stale), MediaDetails.Empty(MediaType.Movie), Run(Everyone, writer), CancellationToken.None);

		await Assert.That(Outcomes(await deps.WrittenAsync(writer))).IsEquivalentTo([(RemoteService.Plex, 0)]);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenAServiceFailsAgain_WaitsSixHours()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		var failedBefore = UnratedByPlex with { Answers = [PlexLeftTheRatings, ProviderAnswer.Failed(RemoteService.Tmdb, new None(), Now.AddHours(-2))] };
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(failedBefore), Run(Everyone, writer)));

		var tmdb = (await deps.WrittenAsync(writer))[Id].Answers.Single(answer => answer.Provider == RemoteService.Tmdb);
		await Assert.That(tmdb.Failures).IsEqualTo(2);
		await Assert.That(tmdb.AskableAt(new HousekeepingOptions().FreshFor)).IsEqualTo(Now.AddHours(6));
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherOfAGapFillHasNoMatch_StoresEveryFieldItFillsAsUnfilled()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new NoMatch(RemoteService.Tmdb));
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Run(Everyone, writer)));

		await Assert
			.That((await deps.WrittenAsync(writer))[Id].Answers)
			.IsEquivalentTo([PlexLeftTheRatings, new ProviderAnswer(RemoteService.Tmdb, Everything, Now)]);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopy_KeepsTheFailureCountOfAServiceItDidNotAskAgain()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		var failedThrice = new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now.AddDays(-2), Failures: 3);
		var stale = new CachedDetails(Complete(), Now.AddDays(-20), Now.AddDays(-2), [failedThrice]);
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer)));

		await Assert.That(Outcomes(await deps.WrittenAsync(writer))).IsEquivalentTo([(RemoteService.Plex, 0), (RemoteService.Tmdb, 3)]);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopyWhenPlexsLaneRefusesBeforeSending_KeepsItsRatingsAndStaysStale()
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.None | MediaFields.Rated([RatingSource.Tmdb]));
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Unavailable(RemoteService.Plex, new None(), false));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Unrated() with { Ratings = TmdbOnly }));
		var stale = new CachedDetails(Complete(), Now.AddDays(-20), Now.AddDays(-20), []);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Ratings).IsEqualTo(EverySource);
		await Assert.That(fetched.Stale).IsTrue();
		await Assert.That((await deps.WrittenAsync(writer))[Id].RefreshedAt).IsEqualTo(stale.RefreshedAt);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopyWhenASkippedServiceHadGivenNothing_RefreshesIt()
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.None | MediaFields.Rated([RatingSource.Tmdb]));
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Unavailable(RemoteService.Plex, new None(), false));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Complete() with { Ratings = TmdbOnly }));
		var stale = new CachedDetails(Complete() with { Ratings = TmdbOnly }, Now.AddDays(-20), Now.AddDays(-20), []);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer)));

		await Assert.That(fetched.Stale).IsFalse();
		await Assert.That((await deps.WrittenAsync(writer))[Id].RefreshedAt).IsEqualTo(Now);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopy_KeepsNothingAServiceThatLastFailedGave()
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.None | MediaFields.Rated([RatingSource.Tmdb]));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Unrated() with { Ratings = TmdbOnly }));
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-20),
			Now.AddMinutes(-30),
			[new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now.AddDays(-20)), ProviderAnswer.Failed(RemoteService.Plex, new None(), Now.AddMinutes(-30))]
		);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Ratings).IsEqualTo(TmdbOnly);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenEveryEnricherIsBlockedPastItsWait_FailsAsUnavailable()
	{
		var deps = Dependencies.Create(capacity: TestCapacity.With(_ => RemoteLoad.Idle with { BlockedUntil = Some.Of(Now.AddMinutes(1)) }));
		await using var writer = deps.Writer();

		var error = await Assert.That(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer))).IsError();

		await Assert.That(error!.CausedBy<RemoteUnavailableException>()).IsSome();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenAnEnricherFaults_FailsAsUnavailable()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any())
			.Returns(new Faulted(RemoteService.Plex, Error.From(new InvalidOperationException("unreadable"))));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new NoMatch(RemoteService.Tmdb));
		await using var writer = deps.Writer();

		var error = await Assert.That(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer))).IsError();

		await Assert.That(error!.CausedBy<RemoteUnavailableException>()).IsSome();
	}

	[Test]
	public async Task FetchAsync_WhenNoEnricherKnowsTheTitle_FailsWithoutBlamingAProvider()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new NoMatch(RemoteService.Plex));
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new NoMatch(RemoteService.Tmdb));
		await using var writer = deps.Writer();

		var error = await Assert.That(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer))).IsError();

		await Assert.That(error!.CausedBy<RemoteUnavailableException>()).IsNone();
	}

	[Test]
	public async Task FetchAsync_ForATitleKnownOnlyByItsImdbId_AsksTmdbByThatId()
	{
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Found<FoundMedia>(new FoundMedia([Tmdb, imdb], Complete())));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(new MediaIdentity(Id, [imdb]), new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details).IsEqualTo(Complete());
		deps.Tmdb.LookUpAsync(imdb, Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForATitleWithATvdbAndAnImdbId_AsksTmdbByTheTvdbId()
	{
		var tvdb = TvdbId.From("81189", MediaType.Movie);
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Complete()));
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(new MediaIdentity(Id, [ImdbId.From("tt1375666", MediaType.Movie), tvdb]), new None(), Run(Everyone, writer)));

		deps.Tmdb.LookUpAsync(tvdb, Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForAGapInATitleKnownOnlyByItsImdbId_AsksTmdbByThatId()
	{
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(new MovieDetails { Ratings = TmdbOnly }));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(new MediaIdentity(Id, [imdb]), Some.Of(UnratedByPlex), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Ratings).IsEqualTo(TmdbOnly);
		deps.Tmdb.LookUpAsync(imdb, Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenRefreshing_StampsBothTimesWithTheTimeOfTheRefresh()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		await using var writer = deps.Writer();

		await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer));

		await Assert.That((await deps.WrittenAsync(writer))[Id].RefreshedAt).IsEqualTo(Now);
		await Assert.That((await deps.WrittenAsync(writer))[Id].AskedAt).IsEqualTo(Now);
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
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Title).IsEqualTo("Inception");
		await Assert.That((await deps.WrittenAsync(writer))[Id].RefreshedAt).IsEqualTo(Now);
		deps.Plex.LookUpAsync(Plex, Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForACopyThatIsNotStale_AsksOnlyTheEnrichersThatHaveNotAnsweredAndPutsTheirAnswersOverTheStoredCopy()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(new MovieDetails { Title = "Other", Ratings = TmdbOnly }));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Run(Everyone, writer)));

		await Assert.That(fetched.Details).IsEqualTo(Unrated() with { Title = "Other", Ratings = TmdbOnly });
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenFillingGaps_KeepsTheRefreshTimeMovesTheAskTimeAndAddsTheNewAnswer()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Unrated() with { Ratings = TmdbOnly }));
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Run(Everyone, writer)));

		var filled = (await deps.WrittenAsync(writer))[Id];
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
	public async Task FetchAsync_WhenTheEnricherOfAGapFillFails_KeepsTheStoredCopyMovesTheAskTimeAndStoresAFailureForIt()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Run(Everyone, writer)));

		var filled = (await deps.WrittenAsync(writer))[Id];
		await Assert.That(filled.RefreshedAt).IsEqualTo(LastRefresh);
		await Assert.That(filled.AskedAt).IsEqualTo(Now);
		await Assert.That(filled.Answers.Select(answer => (answer.Provider, answer.Failures))).IsEquivalentTo([(RemoteService.Plex, 0), (RemoteService.Tmdb, 1)]);
		await Assert.That(filled.Details).IsEqualTo(UnratedByPlex.Details);
	}

	[Test]
	public async Task FetchAsync_WhenFillingGaps_PutsTheSecondAnswerOverTheStoredCopyToo()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null, PosterUrl = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any())
			.Returns(Answer(new MovieDetails { Tagline = "A dream within a dream.", PosterUrl = "tmdb-poster" }));
		var stored = new CachedDetails(Complete() with { Tagline = null, PosterUrl = "stored-poster" }, LastRefresh, LastRefresh, []);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stored), Run(Everyone, writer)));

		await Assert.That(fetched.Details.PosterUrl).IsEqualTo("tmdb-poster");
	}

	[Test]
	public async Task FetchAsync_WhenAnAnswerLacksAFieldTheStoredCopyHas_ListsItAsUnfilledByThatProvider()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Unrated() with { Tagline = null, Ratings = TmdbOnly }));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(UnratedByPlex), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Tagline).IsEqualTo("A dream within a dream.");
		await Assert
			.That((await deps.WrittenAsync(writer))[Id].Answers.Single(answer => answer.Provider == RemoteService.Tmdb).Unfilled.Overlaps(MediaField.Tagline))
			.IsTrue();
	}

	[Test]
	[Arguments(13, "A dream within a dream.")]
	[Arguments(14, null)]
	public async Task FetchAsync_ForAStaleCopy_KeepsAValueTheRefreshLacksOnlyWhileAProviderGaveItWithinTwoWeeks(int daysAgo, string? tagline)
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Complete() with { Tagline = null }));
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-20),
			Now.AddDays(-daysAgo),
			[new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now.AddDays(-daysAgo))]
		);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Tagline).IsEqualTo(tagline);
	}

	[Test]
	public async Task FetchAsync_ForAStaleCopyAnsweredByAnEnricherNoLongerRegistered_KeepsNothingItGave()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Complete() with { Tagline = null }));
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-20),
			Now.AddDays(-1),
			[new ProviderAnswer(RemoteService.Wikidata, MediaFields.None, Now.AddDays(-1))]
		);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Tagline).IsNull();
	}

	[Test]
	[Arguments(3, true)]
	[Arguments(14, false)]
	public async Task FetchAsync_ForAStaleCopyWhilePlexIsDown_AsksPlexAndDropsItsRatingsOnlyOnceItsAnswerIsTwoWeeksOld(int daysAgo, bool kept)
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.None | MediaFields.Rated([RatingSource.Tmdb]));
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Answer(Unrated() with { Ratings = TmdbOnly }));
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-20),
			Now.AddDays(-daysAgo),
			[
				new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now.AddDays(-20)),
				new ProviderAnswer(RemoteService.Plex, MediaFields.None, Now.AddDays(-daysAgo)),
			]
		);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(Everyone, writer)));

		var written = (await deps.WrittenAsync(writer))[Id];
		await Assert.That(fetched.Details.Ratings).IsEqualTo(kept ? EverySource : TmdbOnly);
		await Assert.That(written.Answers.Single(answer => answer.Provider == RemoteService.Plex).Failures).IsEqualTo(kept ? 0 : 1);
		await Assert.That(written.RefreshedAt).IsEqualTo(Now);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Exactly(kept ? 0 : 1));
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
			[new ProviderAnswer(RemoteService.Tmdb, MediaFields.None, Now.AddDays(-14)), PlexLeftTheRatings with { AnsweredAt = Now.AddDays(-14) }]
		);
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, Some.Of(stale), Run(OnlyPlex, writer)));

		await Assert.That(fetched.Details).IsEqualTo(Unrated());
	}

	[Test]
	[Arguments(SearchProvider.Tmdb)]
	[Arguments(SearchProvider.Arr)]
	public async Task FetchAsync_WhenSearchIsNotPlex_AsksTmdbFirstByItsTmdbId(SearchProvider search)
	{
		var deps = Dependencies.Create(search: search);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Found<FoundMedia>(new FoundMedia([.. Identity.Keys], Complete())));
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		deps.Tmdb.LookUpAsync(Tmdb, Any(), Any()).WasCalled(Times.Once);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenSearchIsTmdbAndTheTitleIsOnlyKnownToPlex_AsksPlex()
	{
		var deps = Dependencies.Create(search: SearchProvider.Tmdb);
		deps.PlexAnswers(Complete(), PlexOnly);
		await using var writer = deps.Writer();

		await Matched(await deps.Testee.FetchAsync(PlexOnly, new None(), Run(Everyone, writer)));

		deps.Plex.LookUpAsync(Plex, Any(), Any()).WasCalled(Times.Once);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenPlexIsBrieflyBlockedButHasNoEnrichWait_AsksOnlyTmdb()
	{
		var deps = Dependencies.Create(
			search: SearchProvider.Tmdb,
			capacity: TestCapacity.With(remote => RemoteLoad.Idle with { BlockedUntil = Optional.When(remote is RemoteService.Plex, () => Now.AddMilliseconds(20)) })
		);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Found<FoundMedia>(new FoundMedia([.. Identity.Keys], Unrated())));
		await using var writer = deps.Writer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, new None(), Run(Everyone, writer)));

		await Assert.That(fetched.Details.Missing).IsEqualTo(MediaFields.AllRatings);
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FindReusable_WhenAnEnricherTheUserHasConnectedCanFillAGap_IsNone()
	{
		var deps = Dependencies.Create();
		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(UnratedByPlex), Everyone, FieldNeeds.Everything)).IsNone();
	}

	[Test]
	public async Task FindReusable_WhenTheOnlyEnricherThatCanFillAGapIsNotConnected_IsTheStoredCopy()
	{
		var deps = Dependencies.Create();
		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(UnratedByPlex), OnlyPlex, FieldNeeds.Everything)).IsSome().And.IsEqualTo(UnratedByPlex);
	}

	[Test]
	public async Task FindReusable_WhenTheFeedReadsNoneOfTheGaps_IsTheStoredCopy()
	{
		var deps = Dependencies.Create();
		var readsTheTitle = new FieldNeeds(MediaField.Title, MediaField.Poster);

		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(UnratedByPlex), Everyone, readsTheTitle)).IsSome().And.IsEqualTo(UnratedByPlex);
	}

	[Test]
	public async Task FindReusable_ForAGapOnlyAServiceQueuedPastFiveSecondsFills_ReusesTheCopyOnlyWhenTheGapIsShownOnly()
	{
		var deps = Dependencies.Create(capacity: TmdbQueued(seconds: 6));

		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(UnratedByPlex), Everyone, new FieldNeeds(MediaFields.None, MediaFields.AllRatings))).IsSome();
		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(UnratedByPlex), Everyone, new FieldNeeds(MediaFields.AllRatings, MediaFields.None))).IsNone();
	}

	[Test]
	public async Task LoadAsync_ForAnUncachedTitleWhoseServicesQueuePastFiveSeconds_SettlesWithThePlaceholderAndRecordsNothing()
	{
		var deps = Dependencies.Create(capacity: TestCapacity.With(_ => RemoteLoad.Idle with { EstimatedWait = TimeSpan.FromSeconds(6) }));
		var placeholder = new MovieDetails { Title = "Inception" };
		await using var writer = deps.Writer();

		var loaded = await deps.Testee.LoadAsync(
			new IdentifyHandoff(Identity, new None()),
			placeholder,
			new DetailsRun(Everyone, new FieldNeeds(MediaFields.None, MediaFields.AllRatings), writer),
			CancellationToken.None
		);

		await Assert.That((await Matched(loaded)).Details).IsEqualTo(placeholder);
		await Assert.That(await deps.WrittenAsync(writer)).IsEmpty();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task LoadAsync_ForAFieldTheFeedFiltersOn_AsksAServiceQueuedPastFiveSeconds()
	{
		var deps = Dependencies.Create(capacity: TestCapacity.With(_ => RemoteLoad.Idle with { EstimatedWait = TimeSpan.FromSeconds(6) }));
		deps.PlexAnswers(Complete());
		await using var writer = deps.Writer();

		var loaded = await deps.Testee.LoadAsync(
			new IdentifyHandoff(Identity, new None()),
			MediaDetails.Empty(MediaType.Movie),
			new DetailsRun(Everyone, new FieldNeeds(MediaFields.AllRatings, MediaFields.None), writer),
			CancellationToken.None
		);

		await Assert.That((await Matched(loaded)).Details).IsEqualTo(Complete());
	}

	[Test]
	public async Task LoadAsync_ForAStaleTitleOnAGrid_StillRefreshesIt()
	{
		var deps = Dependencies.Create();
		deps.PlexAnswers(Complete());
		var stale = new CachedDetails(Complete() with { Title = "Old" }, Now.AddDays(-14), Now.AddDays(-14), []);
		await using var writer = deps.Writer();

		var loaded = await deps.Testee.LoadAsync(
			Stored(stale),
			MediaDetails.Empty(MediaType.Movie),
			new DetailsRun(Everyone, FieldNeeds.None, writer),
			CancellationToken.None
		);

		await Assert.That((await Matched(loaded)).Details.Title).IsEqualTo("Inception");
	}

	[Test]
	public async Task LoadAsync_WhenAStaleTitlesRefreshCannotRun_FallsBackToItMarkedStale()
	{
		var deps = Dependencies.Create();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(Down);
		var stale = new CachedDetails(Complete(), Now.AddDays(-14), Now.AddDays(-14), []);
		await using var writer = deps.Writer();

		var loaded = await Matched(await deps.Testee.LoadAsync(Stored(stale), MediaDetails.Empty(MediaType.Movie), Run(Everyone, writer), CancellationToken.None));

		await Assert.That(loaded.Stale).IsTrue();
		await Assert.That(loaded.Details).IsEqualTo(Complete());
	}

	[Test]
	public async Task LoadAsync_ForAStaleTitleWhoseServicesAreBackingOff_FallsBackToItMarkedStale()
	{
		var deps = Dependencies.Create();
		var failedLately = Now.AddMinutes(-30);
		var stale = new CachedDetails(
			Complete(),
			Now.AddDays(-20),
			failedLately,
			[ProviderAnswer.Failed(RemoteService.Plex, new None(), failedLately), ProviderAnswer.Failed(RemoteService.Tmdb, new None(), failedLately)]
		);
		await using var writer = deps.Writer();

		var loaded = await Matched(await deps.Testee.LoadAsync(Stored(stale), MediaDetails.Empty(MediaType.Movie), Run(Everyone, writer), CancellationToken.None));

		await Assert.That(loaded.Stale).IsTrue();
		deps.Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task LoadAsync_WhenAGapFillThrows_FallsBackToTheCopyUnmarked()
	{
		var deps = Dependencies.Create();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Throws(new InvalidOperationException("broken enricher"));
		await using var writer = deps.Writer();

		var loaded = await Matched(
			await deps.Testee.LoadAsync(Stored(UnratedByPlex), MediaDetails.Empty(MediaType.Movie), Run(Everyone, writer), CancellationToken.None)
		);

		await Assert.That(loaded.Details).IsEqualTo(UnratedByPlex.Details);
		await Assert.That(loaded.Stale).IsFalse();
	}

	[Test]
	public async Task LoadAsync_ForAStaleTitleWhoseDetailServiceQueuesPastFiveSeconds_KeepsItsStoredFieldsAndStaysStale()
	{
		var deps = Dependencies.Create(capacity: TmdbQueued(seconds: 6));
		deps.PlexAnswers(new MovieDetails { Ratings = EverySource });
		var stale = new CachedDetails(Complete(), Now.AddDays(-20), Now.AddDays(-20), []);
		await using var writer = deps.Writer();

		var loaded = await Matched(
			await deps.Testee.LoadAsync(Stored(stale), MediaDetails.Empty(MediaType.Movie), new DetailsRun(Everyone, FieldNeeds.None, writer), CancellationToken.None)
		);

		await Assert.That(loaded.Details).IsEqualTo(Complete());
		await Assert.That(loaded.Stale).IsTrue();
		await Assert.That((await deps.WrittenAsync(writer))[Id].RefreshedAt).IsEqualTo(stale.RefreshedAt);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FindReusable_ForATenDayOldCopy_IsNoneOnlyUnderASevenDayFreshPeriod()
	{
		var tenDaysOld = new CachedDetails(Complete(), Now.AddDays(-10), Now.AddDays(-10), []);
		var deps = Dependencies.Create(housekeeping: new HousekeepingOptions { FreshDays = 7 });

		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(tenDaysOld), Everyone, FieldNeeds.Everything)).IsNone();
		await Assert.That(Dependencies.Create().Testee.FindReusable(Identity, Some.Of(tenDaysOld), Everyone, FieldNeeds.Everything)).IsSome();
	}

	[Test]
	public async Task FindReusable_WhileTheOnlyServiceForAGapIsBackingOff_IsTheStoredCopy()
	{
		var deps = Dependencies.Create();
		var backingOff = BackingOff(failures: 2);

		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(backingOff), OnlyPlex, FieldNeeds.Everything)).IsSome().And.IsEqualTo(backingOff);
	}

	[Test]
	public async Task FindReusable_OnceTheBackoffOfTheOnlyServiceForAGapHasEnded_IsNone()
	{
		var deps = Dependencies.Create();

		await Assert.That(deps.Testee.FindReusable(Identity, Some.Of(BackingOff(failures: 1)), OnlyPlex, FieldNeeds.Everything)).IsNone();
	}

	[Test]
	public async Task ConnectedAsync_IsWhatReadinessSaysOfTheEnrichers()
	{
		var deps = Dependencies.Create();

		await Assert.That(await deps.Testee.ConnectedAsync(CancellationToken.None)).IsEquivalentTo([RemoteService.Tmdb, RemoteService.Plex]);
	}

	private static DetailsRun Run(IReadOnlySet<RemoteService> connected, LibraryWriter writer) => new(connected, FieldNeeds.Everything, writer);

	private static IdentifyHandoff Stored(CachedDetails cached) => new(Identity, Some.Of(new StoredMedia(Identity, Some.Of(cached))));

	private static CachedDetails BackingOff(int failures)
	{
		var failedTwoHoursAgo = new ProviderAnswer(RemoteService.Plex, MediaFields.None, Now.AddHours(-2), failures);
		return new CachedDetails(Unrated(), LastRefresh, Now.AddHours(-2), [failedTwoHoursAgo]);
	}

	private static IEnumerable<(RemoteService Provider, int Failures)> Outcomes(IReadOnlyDictionary<MediaId, CachedDetails> written)
	{
		return written[Id].Answers.Select(answer => (answer.Provider, answer.Failures));
	}

	private static async Task<MatchedMedia> Matched(Result<MatchedMedia> result)
	{
		var matched = await Assert.That(result).IsSuccess();
		return matched!;
	}

	private static IRemoteCapacity TmdbQueued(int seconds)
	{
		return TestCapacity.With(remote => RemoteLoad.Idle with { EstimatedWait = remote is RemoteService.Tmdb ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero });
	}

	private static IRemoteCapacity TmdbBlocked(Func<DateTimeOffset> until)
	{
		return TestCapacity.With(remote =>
			RemoteLoad.Idle with
			{
				BlockedUntil = Optional.When(remote is RemoteService.Tmdb, until),
				EnrichWait = TimeSpan.FromSeconds(1),
			}
		);
	}

	private static Answer<FoundMedia> Answer(MediaDetails details)
	{
		return new Found<FoundMedia>(new FoundMedia([Tmdb], details));
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

	private sealed record Dependencies(Mock<IMediaRepository> MediaRepository, Mock<IDetailsEnricher> Plex, Mock<IDetailsEnricher> Tmdb, DetailsFetcher Testee)
	{
		public static Dependencies Create(
			MediaFields? tmdbFills = null,
			IRemoteCapacity? capacity = null,
			SearchProvider search = SearchProvider.Plex,
			IArtworkResizer? resizer = null,
			HousekeepingOptions? housekeeping = null
		)
		{
			var mediaRepository = IMediaRepository.Mock();
			var plex = IDetailsEnricher.Mock();
			plex.Fills.Returns(Everything);
			plex.Accepts.Returns([MediaProvider.Plex]);
			var tmdb = IDetailsEnricher.Mock();
			tmdb.Fills.Returns(tmdbFills ?? Everything);
			tmdb.Accepts.Returns([MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Imdb]);

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
				TestReadiness.Everything(),
				[resizer ?? TestArtwork.ResizesAnything()],
				Clock(),
				new FeedLoadOptions(),
				housekeeping ?? new HousekeepingOptions(),
				NullLogger<DetailsFetcher>.Instance
			);
			return new Dependencies(mediaRepository, plex, tmdb, testee);
		}

		public void PlexAnswers(MovieDetails details, MediaIdentity? identity = null)
		{
			Plex.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Found<FoundMedia>(new FoundMedia([.. (identity ?? Identity).Keys], details)));
		}

		private static IDateTimeProvider Clock()
		{
			var clock = IDateTimeProvider.Mock();
			clock.Now.Returns(Now);
			return clock.Object;
		}

		public LibraryWriter Writer() => new(MediaRepository.Object, NullLogger<LibraryWriter>.Instance);

		public async Task<IReadOnlyDictionary<MediaId, CachedDetails>> WrittenAsync(LibraryWriter writer)
		{
			await writer.DisposeAsync();
			return Mock.Invocations(MediaRepository)
				.Select(call => call.Arguments[0])
				.OfType<IReadOnlyList<DetailsChange>>()
				.SelectMany(changes => changes)
				.GroupBy(change => change.Id)
				.ToDictionary(group => group.Key, group => group.Last().After);
		}
	}
}
