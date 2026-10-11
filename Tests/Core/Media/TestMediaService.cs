using Contracts;
using Contracts.Caching;
using Contracts.DI;
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
using Shared.Options;

namespace Core.Tests.Media;

internal static class TestMediaService
{
	internal static readonly DateTimeOffset Now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

	internal static (IMediaService Testee, Deps Deps) Create() => Create([], []);

	internal static (IMediaService Testee, Deps Deps) Create(
		IReadOnlyList<IBurstPrimer> burstPrimers,
		IReadOnlyList<Keyed<RemoteService, IExternalIdConverter>> converters
	)
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

		var claims = TestClaims.Granting();
		var detailsFetcher = new DetailsFetcher(
			enrichers,
			claims.Object,
			configRepo.Object,
			TestCapacity.Open(),
			TestReadiness.Everything(),
			[TestArtwork.ResizesAnything()],
			dateTimeProvider.Object,
			new FeedLoadOptions(),
			new HousekeepingOptions(),
			NullLogger<DetailsFetcher>.Instance
		);
		var resolver = new MediaIdResolver(
			[new(RemoteService.Tmdb, converter.Object), .. converters],
			unmatchedCache.Object,
			TestCapacity.Open(),
			idReadiness.Object,
			dateTimeProvider.Object,
			NullLogger<MediaIdResolver>.Instance
		);
		var identifier = new TitleIdentifier(resolver, claims.Object);
		var writers = new LibraryWriterFactory(mediaRepo.Object, NullLogger<LibraryWriter>.Instance);
		var primers = new BurstPrimers(burstPrimers, detailsFetcher, TestCapacity.Open(), dateTimeProvider.Object, new FeedLoadOptions());
		var sink = TestSinks.AddressingBy(MediaProvider.Tmdb, configRepo.Object);
		var testee = new MediaService(
			resolver,
			identifier,
			detailsFetcher,
			new ListLoad(identifier, resolver, detailsFetcher, writers, sink, mediaRepo.Object, primers, new FeedLoadOptions(), TimeProvider.System),
			claims.Object,
			writers,
			sink,
			mediaRepo.Object,
			primers
		);
		return (testee, new Deps(mediaRepo, details, unmatchedCache, converter, idReadiness, claims));
	}

	internal sealed record Deps(
		Mock<IMediaRepository> MediaRepo,
		Mock<IDetailsEnricher> Details,
		Mock<ITypedMemoryCache<string, bool>> UnmatchedCache,
		Mock<IExternalIdConverter> Converter,
		Mock<IServiceReadiness> IdReadiness,
		Mock<IIdentityClaims> Claims
	);

	internal static Answer<IdLookupResult> FoundByFind(IdLookupRequest request)
	{
		return request switch
		{
			ByIdRequest(_, var id) => new Found<IdLookupResult>(new IdLookupResult([TmdbId.From($"found-{id.Value}", id.Type)], new None())),
			ByHintsRequest => new NoMatch(RemoteService.Tmdb),
		};
	}

	internal static IAsyncEnumerable<IdentifyRequest> Requests(int count)
	{
		return Enumerable.Range(0, count).Select(i => new IdentifyRequest([TmdbId.From(i.ToString(), MediaType.Movie)], new None())).ToAsyncEnumerable();
	}

	internal static async IAsyncEnumerable<IdentifyRequest> Paged(params int[] pages)
	{
		var next = 0;
		foreach (var size in pages)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(20));
			foreach (var _ in Enumerable.Range(0, size))
			{
				yield return new IdentifyRequest([TmdbId.From($"{next++}", MediaType.Movie)], new None());
			}
		}
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
