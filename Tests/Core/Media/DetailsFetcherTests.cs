using Contracts;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Providers;
using Contracts.Settings;
using Core.Media;
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
	private static readonly RatingSummary TmdbRating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8f, Some.Of(500))]);
	private static readonly Error Unavailable = Error.From(new ProviderUnavailableException(MediaProvider.Tmdb));
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatRatesHasNoRating_AsksItOnceAndConfirmsTheGap()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails()));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Rating);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatRatesIsUnavailable_LeavesTheRatingUnconfirmed()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		await Assert.That(fetched.Details.Missing).IsEqualTo(MediaField.Rating);
		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.None);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherCannotFillAnyMissingField_NeverCallsIt()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Poster);
		deps.Primary(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherCanFillAMissingField_CallsItAndMergesWhatItFound()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Tagline);
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Tagline = "Your mind is the scene of the crime." }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		await Assert.That(fetched.Details.Tagline).IsEqualTo("Your mind is the scene of the crime.");
	}

	[Test]
	public async Task FetchAsync_WhenOnlyTheRatingIsMissing_TakesItFromAnEnricherThatRates()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Rating = TmdbRating }));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		await Assert.That(fetched.Details.Rating).IsEqualTo(TmdbRating);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_ForACompletePrimaryResult_AsksNoEnricher()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenOnlyTheRatingIsMissing_NeverAsksAnEnricherThatCannotRate()
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.Rating);
		deps.Primary(Unrated());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenAGapSurvivesEveryEnricher_RecordsItAsConfirmed()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails()));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Tagline);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherAnswersThatItHasNothing_ConfirmsTheGap()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("TMDB detail returned 404 for 500"));
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Tagline);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherAnswersAnotherType_IgnoresTheAnswerAndConfirmsTheGap()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Success.Of(new FoundMedia([TvdbId.From("81189", MediaType.Show)], new ShowDetails { Tagline = "Say my name." })));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		await Assert.That(fetched.Identity).IsEqualTo(Identity);
		await Assert.That(fetched.Details).IsEqualTo(Complete() with { Tagline = null });
		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Tagline);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherIsUnavailable_LeavesOnlyItsFieldsUnconfirmed()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Tagline);
		deps.Primary(Complete() with { Tagline = null, Summary = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Summary);
	}

	[Test]
	public async Task FetchAsync_WhenTheTitleHasNoIdForTheEnricher_ConfirmsTheGapWithoutAsking()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null }, PlexOnly);
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(PlexOnly, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Tagline);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherThatRatesIsRateLimitedAndTheRatingIsMissing_StillAsksItOnce()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(provider => new ProviderLoad(provider is MediaProvider.Tmdb ? Some.Of(Now.AddMinutes(1)) : new None(), 0))
		);
		deps.Primary(Unrated() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Rating = TmdbRating }));
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task FetchAsync_WhenTheEnricherIsRateLimited_SkipsItAndLeavesItsFieldsUnconfirmed()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(provider => new ProviderLoad(provider is MediaProvider.Tmdb ? Some.Of(Now.AddMinutes(1)) : new None(), 0))
		);
		deps.Primary(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await Matched(await deps.Testee.FetchAsync(Identity, buffer));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.None);
	}

	[Test]
	public async Task FetchAsync_WhenAnEnricherAnswersACompleteRecord_KeepsTheTitlesMediaIdAndAddsTheIdsItFound()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia([Tmdb, imdb], Complete())));
		await using var buffer = deps.Buffer();

		var fetched = await Matched(await deps.Testee.FetchAsync(Identity, buffer));

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

		var fetched = await deps.Testee.FetchAsync(Identity, buffer);

		await Assert.That(fetched).IsError();
		await Assert.That(buffer.NewDetails).IsEmpty();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task FetchAsync_WhenTheDetailsAreFetched_StampsThemWithTheTimeOfTheFetch()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete());
		await using var buffer = deps.Buffer();

		await deps.Testee.FetchAsync(Identity, buffer);

		await Assert.That(buffer.NewDetails[Id].FetchedAt).IsEqualTo(Now);
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
			Rating = TmdbRating,
			Runtime = TimeSpan.FromMinutes(148),
		};
	}

	private static MovieDetails Unrated() => Complete() with { Rating = RatingSummary.None };

	private sealed record Dependencies(
		Mock<IMediaRepository> MediaRepository,
		Mock<IMediaSearchProvider> Search,
		Mock<IDetailsEnricher> Tmdb,
		DetailsFetcher Testee
	)
	{
		public static Dependencies Create(MediaField tmdbFills = ~MediaField.None, IProviderCapacity? capacity = null)
		{
			var mediaRepository = IMediaRepository.Mock();
			mediaRepository.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
			var search = IMediaSearchProvider.Mock();
			var tmdb = IDetailsEnricher.Mock();
			tmdb.Fills.Returns(tmdbFills);

			var config = IProviderConfigService.Mock();
			config
				.GetAsync(Any())
				.Returns(
					new ProviderConfig
					{
						Sink = SinkKind.Plex,
						TrendingProvider = TrendingProvider.Tmdb,
						SearchProvider = SearchProvider.Plex,
						AuthProvider = AuthProvider.Plex,
						RegistrationMode = RegistrationMode.Open,
					}
				);

			var testee = new DetailsFetcher(
				new FakeKeyedServiceIndex<SearchProvider, IMediaSearchProvider>().Register(SearchProvider.Plex, search.Object),
				new FakeKeyedServiceIndex<MediaProvider, IDetailsEnricher>()
					.Register(MediaProvider.Plex, IDetailsEnricher.Mock().Object)
					.Register(MediaProvider.Tmdb, tmdb.Object),
				config.Object,
				capacity ?? TestCapacity.Open(),
				Clock()
			);
			return new Dependencies(mediaRepository, search, tmdb, testee);
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
