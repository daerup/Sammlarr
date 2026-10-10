using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Providers;
using Contracts.Settings;
using Core.DI;
using Core.Media;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Media;

internal static class TestMediaService
{
	internal static readonly DateTimeOffset Now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

	internal static (IMediaService Testee, Deps Deps) Create()
	{
		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(Now);
		var mediaRepo = IMediaRepository.Mock();
		var details = IDetailsEnricher.Mock();
		details.Fills.Returns(~MediaField.None | MediaFields.AllRatings);
		details.Accepts.Returns([.. Enum.GetValues<MediaProvider>()]);
		var idle = IDetailsEnricher.Mock();
		idle.Fills.Returns(MediaFields.None);
		idle.Accepts.Returns([]);
		var unmatchedCache = TestUnmatchedCache.Empty();

		mediaRepo
			.FindIdentitiesAsync(Any<IReadOnlyList<IReadOnlyList<IExternalMediaId>>>(), Any())
			.Returns((groups, _) => [.. Enumerable.Repeat<Optional<MediaIdentity>>(new None(), groups.Count)]);
		mediaRepo.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
		mediaRepo.GetAsync(Any(), Any()).Returns(Array.Empty<StoredMedia>());
		mediaRepo.GetByExternalsAsync(Any(), Any()).Returns(Array.Empty<StoredMedia>());

		var configRepo = IProviderConfigService.Mock();
		configRepo
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

		var enrichers = new KeyedServiceSet<RemoteService, IDetailsEnricher>([new(RemoteService.Plex, details.Object), new(RemoteService.Tmdb, idle.Object)]);

		var idReadiness = TestReadiness.EverythingMock();

		var converter = IExternalIdConverter.Mock();
		converter.Lookups.Returns([new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tmdb], Cost: 1)]);
		converter.LookUpAsync(Any(), Any()).Returns((request, _) => FoundByFind(request));

		var detailsFetcher = new DetailsFetcher(
			enrichers,
			configRepo.Object,
			TestCapacity.Open(),
			TestReadiness.Everything(),
			[TestArtwork.ResizesAnything()],
			dateTimeProvider.Object,
			NullLogger<DetailsFetcher>.Instance
		);
		var coordinator = new MediaIdentityCoordinator(mediaRepo.Object, NullLogger<MediaIdentityCoordinator>.Instance);
		var testee = new MediaService(
			new MediaIdResolver([new(RemoteService.Tmdb, converter.Object)], unmatchedCache.Object, TestCapacity.Open(), idReadiness.Object, dateTimeProvider.Object),
			detailsFetcher,
			coordinator,
			new MediaWriteBufferFactory(mediaRepo.Object, NullLogger<MediaWriteBuffer>.Instance),
			TestSinks.AddressingBy(MediaProvider.Tmdb, configRepo.Object),
			mediaRepo.Object,
			NullLogger<MediaService>.Instance
		);
		return (testee, new Deps(mediaRepo, details, unmatchedCache, converter, idReadiness));
	}

	internal sealed record Deps(
		Mock<IMediaRepository> MediaRepo,
		Mock<IDetailsEnricher> Details,
		Mock<ITypedMemoryCache<string, bool>> UnmatchedCache,
		Mock<IExternalIdConverter> Converter,
		Mock<IServiceReadiness> IdReadiness
	);

	internal static Result<IdLookupResult> FoundByFind(IdLookupRequest request)
	{
		return request switch
		{
			ByIdRequest(_, var id) => Success.Of(new IdLookupResult([TmdbId.From($"found-{id.Value}", id.Type)], new None())),
			ByHintsRequest => Error.From("The test converter offers no search."),
		};
	}

	internal static IAsyncEnumerable<IdentifyRequest> Requests(int count)
	{
		return Enumerable.Range(0, count).Select(i => new IdentifyRequest([TmdbId.From(i.ToString(), MediaType.Movie)], new None())).ToAsyncEnumerable();
	}

	internal static FoundMedia CompleteMovie(IExternalMediaId external)
	{
		return new(
			[external],
			new MovieDetails
			{
				Title = "T",
				Tagline = "T",
				Summary = "T",
				PosterUrl = "https://example.com/p.jpg",
				BackdropUrl = "https://example.com/b.jpg",
				ReleaseDate = new DateOnly(2024, 1, 1),
				Genres = ["Action"],
				Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(100))]),
				Runtime = TimeSpan.FromMinutes(90),
			}
		);
	}
}
