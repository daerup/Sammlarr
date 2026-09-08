using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Core.Media;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Media;

public class MediaServiceTests
{
	private static readonly DateTimeOffset _now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task IdentifyAsync_YieldsOneResultPerInput()
	{
		var first = TmdbId.From("1", MediaType.Movie);
		var second = TmdbId.From("2", MediaType.Movie);
		var third = TmdbId.From("3", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any())
			.Returns([
				FreshCached(MediaId.From(Guid.NewGuid()), first),
				FreshCached(MediaId.From(Guid.NewGuid()), second),
				FreshCached(MediaId.From(Guid.NewGuid()), third),
			]);

		var results = await testee
			.IdentifyAsync([
				new ResolveRequest([first], MediaProvider.Tmdb, new None()),
				new ResolveRequest([second], MediaProvider.Tmdb, new None()),
				new ResolveRequest([third], MediaProvider.Tmdb, new None()),
			])
			.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(3);
		foreach (var result in results)
		{
			await Assert.That(result).IsSuccess();
		}
	}

	[Test]
	public async Task IdentifyAsync_IssuesSingleBatchedMetadataQuery()
	{
		var first = TmdbId.From("10", MediaType.Movie);
		var second = TmdbId.From("11", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any())
			.Returns([FreshCached(MediaId.From(Guid.NewGuid()), first), FreshCached(MediaId.From(Guid.NewGuid()), second)]);

		await testee
			.IdentifyAsync([new ResolveRequest([first], MediaProvider.Tmdb, new None()), new ResolveRequest([second], MediaProvider.Tmdb, new None())])
			.ToListAsync();

		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_YieldsOneResultPerInput()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var firstDetail = BuildDetail(firstId);
		var secondDetail = BuildDetail(secondId);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([new CachedMedia(firstDetail, _now), new CachedMedia(secondDetail, _now)]);

		var results = await testee.GetDetailsAsync([firstId, secondId]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0]).IsOfUnionType<Result<IdentifiedMedia>, Movie>().And.IsSameReferenceAs(firstDetail);
		await Assert.That(results[1]).IsOfUnionType<Result<IdentifiedMedia>, Movie>().And.IsSameReferenceAs(secondDetail);
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenMetadataMissing_YieldsError()
	{
		var id = MediaId.From(Guid.NewGuid());
		var (testee, _) = CreateTestee();

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task GetDetailsAsync_ById_IssuesSingleJoinedReadCall()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([new CachedMedia(BuildDetail(firstId), _now), new CachedMedia(BuildDetail(secondId), _now)]);

		await testee.GetDetailsAsync([firstId, secondId]).ToListAsync();

		deps.MediaRepo.GetAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_OnMisses_FlushesMetadataUpsertOnceAtEnd()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var thirdId = MediaId.From(Guid.NewGuid());
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([StaleCached(firstId), StaleCached(secondId), StaleCached(thirdId)]);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(CompleteMovie(ext).ToMatch()));

		await testee.GetDetailsAsync([firstId, secondId, thirdId]).ToListAsync();

		deps.MediaRepo.UpsertMetadataAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ByRequest_IdentifiesAndFetches_YieldingMetadataPerRequest()
	{
		var firstExternal = TmdbId.From("100", MediaType.Movie);
		var secondExternal = TmdbId.From("200", MediaType.Movie);
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var firstDetail = BuildDetail(firstId, firstExternal);
		var secondDetail = BuildDetail(secondId, secondExternal);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).Returns([new CachedMedia(firstDetail, _now), new CachedMedia(secondDetail, _now)]);

		var results = await testee
			.ResolveDetailsAsync([
				new ResolveRequest([firstExternal], MediaProvider.Tmdb, new None()),
				new ResolveRequest([secondExternal], MediaProvider.Tmdb, new None()),
			])
			.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0]).IsOfUnionType<MediaLookup, Movie>().And.IsSameReferenceAs(firstDetail);
		await Assert.That(results[1]).IsOfUnionType<MediaLookup, Movie>().And.IsSameReferenceAs(secondDetail);
	}

	[Test]
	public async Task ResolveDetailsAsync_WhenIdentifyFails_YieldsUnmatchedWithoutFetching()
	{
		var external = TmdbId.From("999", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		deps.UnresolvedCache.TryGetValue(Any<string>()).Returns(true).SetsOutValue(true);

		var results = await testee.ResolveDetailsAsync([new ResolveRequest([external], MediaProvider.Tmdb, new None())]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0]).IsOfUnionType<MediaLookup, UnmatchedMedia>();
		deps.MediaRepo.UpsertMetadataAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task ResolveIdentitiesAsync_WhenEmpty_DoesNotWriteToRepository()
	{
		var (testee, deps) = CreateTestee();

		var identities = await testee.ResolveIdentitiesAsync([]);

		await Assert.That(identities).IsEmpty();
		deps.MediaRepo.UpsertIdentitiesAsync(Any(), Any()).WasNeverCalled();
	}

	private static (IMediaService Testee, Deps Deps) CreateTestee()
	{
		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(_now);
		var mediaRepo = IMediaRepository.Mock();
		var search = IMediaSearchProvider.Mock();
		var unresolvedCache = ITypedMemoryCache<string, bool>.Mock();

		mediaRepo.FindIdentitiesAsync(Any<IReadOnlyList<IReadOnlyList<IExternalMediaId>>>(), Any()).Returns((groups, _) => new MediaIdentity?[groups.Count]);
		mediaRepo.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
		mediaRepo.GetAsync(Any(), Any()).Returns(Array.Empty<CachedMedia>());
		mediaRepo.GetByExternalsAsync(Any(), Any()).Returns(Array.Empty<CachedMedia>());

		var enricher = IMetadataEnricher.Mock();
		var rating = IMediaRatingService.Mock();
		var configRepo = IProviderConfigRepository.Mock();
		configRepo
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);

		var searchIndex = new FakeKeyedServiceIndex<SearchProvider, IMediaSearchProvider>().Register(SearchProvider.Plex, search.Object);
		var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IMetadataEnricher>()
			.Register(MediaProvider.Plex, enricher.Object)
			.Register(MediaProvider.Tmdb, enricher.Object);
		var ratingIndex = new FakeKeyedServiceIndex<RatingProvider, IMediaRatingService>().Register(RatingProvider.Tmdb, rating.Object);

		var metadataResolver = new MediaMetadataResolver(searchIndex, enricherIndex, ratingIndex, configRepo.Object);
		var coordinator = new MediaIdentityCoordinator(mediaRepo.Object);
		var testee = new MediaService(
			new MediaIdResolver(null, unresolvedCache.Object),
			metadataResolver,
			coordinator,
			new MediaResolutionBufferFactory(mediaRepo.Object, NullLogger<MediaResolutionBuffer>.Instance),
			mediaRepo.Object,
			dateTimeProvider.Object,
			NullLogger<MediaService>.Instance
		);
		return (testee, new Deps(mediaRepo, search, unresolvedCache));
	}

	private record Deps(Mock<IMediaRepository> MediaRepo, Mock<IMediaSearchProvider> Search, Mock<ITypedMemoryCache<string, bool>> UnresolvedCache);

	private static CachedMedia FreshCached(MediaId id, IExternalMediaId external) => new(new PartialMovie { Identity = new MediaIdentity(id, [external]) }, _now);

	private static CachedMedia StaleCached(MediaId id) =>
		new(new PartialMovie { Identity = new MediaIdentity(id, [PlexId.From($"plex-{id}", MediaType.Movie)]) }, null);

	private static Movie BuildDetail(MediaId id, IExternalMediaId? extra = null)
	{
		var plex = PlexId.From($"plex-{id}", MediaType.Movie);
		return new()
		{
			Identity = new MediaIdentity(id, extra is null ? [plex] : [plex, extra]),
			Title = "Test",
			Tagline = "Tag",
			Summary = "Summary",
			PosterUrl = "https://example.com/p.jpg",
			BackdropUrl = "https://example.com/b.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};
	}

	private static Movie CompleteMovie(IExternalMediaId external) =>
		new()
		{
			Identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [external]),
			Title = "T",
			Tagline = "T",
			Summary = "T",
			PosterUrl = "https://example.com/p.jpg",
			BackdropUrl = "https://example.com/b.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(90),
		};
}
