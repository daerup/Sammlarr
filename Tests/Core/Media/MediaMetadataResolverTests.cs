using Contracts;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Providers;
using Contracts.Settings;
using Core.Media;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Media;

public class MediaMetadataResolverTests
{
	private static readonly MediaId Id = MediaId.From(Guid.NewGuid());
	private static readonly PlexId Plex = PlexId.From("plex-rating", MediaType.Movie);
	private static readonly TmdbId Tmdb = TmdbId.From("500", MediaType.Movie);
	private static readonly MediaIdentity Identity = new(Id, [Plex, Tmdb]);
	private static readonly MediaIdentity PlexOnly = new(Id, [Plex]);
	private static readonly RatingSummary TmdbRating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 8f, 500)]);
	private static readonly Error Unavailable = Error.From(new ProviderUnavailableException(MediaProvider.Tmdb));
	private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task ResolveAsync_WhenTheEnricherThatRatesHasNoRating_AsksItOnceAndConfirmsTheGap()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails()));
		await using var buffer = deps.Buffer();

		await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Rating);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheEnricherThatRatesIsUnavailable_LeavesTheRatingUnconfirmed()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		var resolved = await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(resolved.Details.Missing).IsEqualTo(MediaField.Rating);
		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.None);
	}

	[Test]
	public async Task ResolveAsync_WhenTheEnricherCannotFillAnyMissingField_NeverCallsIt()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Poster);
		deps.Primary(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await deps.Testee.ResolveAsync(Identity, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ResolveAsync_WhenTheEnricherCanFillAMissingField_CallsItAndMergesWhatItFound()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Tagline);
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Tagline = "Your mind is the scene of the crime." }));
		await using var buffer = deps.Buffer();

		var resolved = await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(resolved.Details.Tagline).IsEqualTo("Your mind is the scene of the crime.");
	}

	[Test]
	public async Task ResolveAsync_WhenOnlyTheRatingIsMissing_TakesItFromAnEnricherThatRates()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Rating = TmdbRating }));
		await using var buffer = deps.Buffer();

		var resolved = await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(resolved.Details.Rating).IsEqualTo(TmdbRating);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_ForACompletePrimaryResult_AsksNoEnricher()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete());
		await using var buffer = deps.Buffer();

		await deps.Testee.ResolveAsync(Identity, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ResolveAsync_WhenOnlyTheRatingIsMissing_NeverAsksAnEnricherThatCannotRate()
	{
		var deps = Dependencies.Create(tmdbFills: ~MediaField.Rating);
		deps.Primary(Unrated());
		await using var buffer = deps.Buffer();

		await deps.Testee.ResolveAsync(Identity, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ResolveAsync_WhenAGapSurvivesEveryEnricher_RecordsItAsConfirmed()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails()));
		await using var buffer = deps.Buffer();

		await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Tagline);
	}

	[Test]
	public async Task ResolveAsync_WhenTheEnricherAnswersThatItHasNothing_ConfirmsTheGap()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("TMDB detail returned 404 for 500"));
		await using var buffer = deps.Buffer();

		await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Tagline);
	}

	[Test]
	public async Task ResolveAsync_WhenTheEnricherAnswersAnotherType_IgnoresTheAnswerAndConfirmsTheGap()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Success.Of(new FoundMedia([TvdbId.From("81189", MediaType.Show)], new ShowDetails { Tagline = "Say my name." })));
		await using var buffer = deps.Buffer();

		var resolved = await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(resolved.Identity).IsEqualTo(Identity);
		await Assert.That(resolved.Details).IsEqualTo(Complete() with { Tagline = null });
		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Tagline);
	}

	[Test]
	public async Task ResolveAsync_WhenTheEnricherIsUnavailable_LeavesOnlyItsFieldsUnconfirmed()
	{
		var deps = Dependencies.Create(tmdbFills: MediaField.Tagline);
		deps.Primary(Complete() with { Tagline = null, Summary = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Unavailable);
		await using var buffer = deps.Buffer();

		await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Summary);
	}

	[Test]
	public async Task ResolveAsync_WhenTheTitleHasNoIdForTheEnricher_ConfirmsTheGapWithoutAsking()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete() with { Tagline = null }, PlexOnly);
		await using var buffer = deps.Buffer();

		await Identified(await deps.Testee.ResolveAsync(PlexOnly, buffer));

		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.Tagline);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ResolveAsync_WhenTheEnricherThatRatesIsRateLimitedAndTheRatingIsMissing_StillAsksItOnce()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(provider => new ProviderLoad(provider is MediaProvider.Tmdb ? Some.Of(Now.AddMinutes(1)) : new None(), 0))
		);
		deps.Primary(Unrated() with { Tagline = null });
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Answer(new MovieDetails { Rating = TmdbRating }));
		await using var buffer = deps.Buffer();

		await deps.Testee.ResolveAsync(Identity, buffer);

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ResolveAsync_WhenTheEnricherIsRateLimited_SkipsItAndLeavesItsFieldsUnconfirmed()
	{
		var deps = Dependencies.Create(
			capacity: TestCapacity.With(provider => new ProviderLoad(provider is MediaProvider.Tmdb ? Some.Of(Now.AddMinutes(1)) : new None(), 0))
		);
		deps.Primary(Complete() with { Tagline = null });
		await using var buffer = deps.Buffer();

		await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
		await Assert.That(buffer.NewDetails[Id].ConfirmedAbsent).IsEqualTo(MediaField.None);
	}

	[Test]
	public async Task ResolveAsync_WhenAnEnricherAnswersACompleteRecord_KeepsTheTitlesMediaIdAndAddsTheIdsItFound()
	{
		var deps = Dependencies.Create();
		deps.Primary(Unrated());
		var imdb = ImdbId.From("tt1375666", MediaType.Movie);
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(new FoundMedia([Tmdb, imdb], Complete())));
		await using var buffer = deps.Buffer();

		var resolved = await Identified(await deps.Testee.ResolveAsync(Identity, buffer));

		await Assert.That(resolved.Identity.Id).IsEqualTo(Id);
		await Assert.That(resolved.Identity.Externals).IsEquivalentTo(new IExternalMediaId[] { Plex, Tmdb, imdb });
	}

	[Test]
	public async Task ResolveAsync_WhenThePrimaryAnswersAnotherType_FailsAndAsksNoEnricher()
	{
		var deps = Dependencies.Create();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any())
			.Returns(Success.Of(new FoundMedia([PlexId.From("plex-rating", MediaType.Show)], new ShowDetails { Title = "Inception" })));
		await using var buffer = deps.Buffer();

		var resolved = await deps.Testee.ResolveAsync(Identity, buffer);

		await Assert.That(resolved).IsError();
		await Assert.That(buffer.NewDetails).IsEmpty();
		deps.Tmdb.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task ResolveAsync_WhenTheDetailsAreFetched_StampsThemWithTheTimeOfTheFetch()
	{
		var deps = Dependencies.Create();
		deps.Primary(Complete());
		await using var buffer = deps.Buffer();

		await deps.Testee.ResolveAsync(Identity, buffer);

		await Assert.That(buffer.NewDetails[Id].FetchedAt).IsEqualTo(Now);
	}

	private static async Task<IdentifiedMedia> Identified(Result<IdentifiedMedia> result)
	{
		var identified = await Assert.That(result).IsSuccess();
		return identified!;
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
		Mock<IMetadataEnricher> Tmdb,
		MediaMetadataResolver Testee
	)
	{
		public static Dependencies Create(MediaField tmdbFills = ~MediaField.None, IProviderCapacity? capacity = null)
		{
			var mediaRepository = IMediaRepository.Mock();
			mediaRepository.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
			var search = IMediaSearchProvider.Mock();
			var tmdb = IMetadataEnricher.Mock();
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

			var testee = new MediaMetadataResolver(
				new FakeKeyedServiceIndex<SearchProvider, IMediaSearchProvider>().Register(SearchProvider.Plex, search.Object),
				new FakeKeyedServiceIndex<MediaProvider, IMetadataEnricher>()
					.Register(MediaProvider.Plex, IMetadataEnricher.Mock().Object)
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

		public MediaResolutionBuffer Buffer() => new(MediaRepository.Object, NullLogger<MediaResolutionBuffer>.Instance);
	}
}
