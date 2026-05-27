using Contracts;
using Contracts.Auth;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Core.Media;
using Shared;
using Test.Shared;
using TUnit.Assertions.Enums;

namespace Core.Tests.Unit.Media;

public class MediaServiceOrderingTests
{
	private static readonly DateTimeOffset _now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task IdentifyAsync_PreservesInputOrder()
	{
		var builder = RequestBuilder.Requests(5);
		var testee = CreateTestee(builder);

		var results = await testee.IdentifyAsync(builder.AsRequests()).ToListAsync();

		await Assert.That(results.ExtractTargetExternals()).IsInOrder();
	}

	[Test]
	public async Task IdentifyAsync_PreservesOrder_WhenSomeFail()
	{
		var builder = RequestBuilder.Requests(6).FailResolveAt(2, 4, 6);
		var testee = CreateTestee(builder);

		var results = await testee.IdentifyAsync(builder.AsRequests()).ToListAsync();

		await Assert.That(results.ExtractTargetExternals()).IsInOrder();
	}

	[Test]
	public async Task IdentifyAsync_PreservesOrder_WhenBatchExceedsConcurrencyLimit()
	{
		var batchSize = IMediaService.RecommendedConcurrency * 3;
		var builder = RequestBuilder.Requests(batchSize);
		var testee = CreateTestee(builder);

		var results = await testee.IdentifyAsync(builder.AsRequests()).ToListAsync();

		await Assert.That(results.ExtractTargetExternals()).IsInOrder();
	}

	[Test]
	public async Task GetDetailsAsync_ById_PreservesInputOrder()
	{
		var builder = RequestBuilder.Requests(5);
		var testee = CreateTestee(builder);

		var results = await testee.GetDetailsAsync(builder.AsMediaIds()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetDetailsAsync_ById_PreservesOrder_WhenSomeFail()
	{
		var builder = RequestBuilder.Requests(6).FailRealizeAt(2, 4, 6);
		var testee = CreateTestee(builder);

		var results = await testee.GetDetailsAsync(builder.AsMediaIds()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetDetailsAsync_ById_PreservesOrder_WhenBatchExceedsConcurrencyLimit()
	{
		var batchSize = IMediaService.RecommendedConcurrency * 3;
		var builder = RequestBuilder.Requests(batchSize);
		var testee = CreateTestee(builder);

		var results = await testee.GetDetailsAsync(builder.AsMediaIds()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetDetailsAsync_ByRequest_PreservesInputOrder()
	{
		var builder = RequestBuilder.Requests(5);
		var testee = CreateTestee(builder);

		var results = await testee.GetDetailsAsync(builder.AsRequests()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetDetailsAsync_ByRequest_PreservesOrder_WhenSomeIdentifyFail()
	{
		var builder = RequestBuilder.Requests(6).FailResolveAt(2, 4, 6);
		var testee = CreateTestee(builder);

		var results = await testee.GetDetailsAsync(builder.AsRequests()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetDetailsAsync_ByRequest_PreservesOrder_WhenBatchExceedsConcurrencyLimit()
	{
		var batchSize = IMediaService.RecommendedConcurrency * 3;
		var builder = RequestBuilder.Requests(batchSize);
		var testee = CreateTestee(builder);

		var results = await testee.GetDetailsAsync(builder.AsRequests()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	private static IMediaService CreateTestee(RequestBuilder builder)
	{
		var mediaRepo = IMediaRepository.Mock();
		var metaRepo = IMediaMetadataRepository.Mock();
		var unresolvedCache = ITypedMemoryCache<string, bool>.Mock();
		var session = IMediaQuerySession.Mock();
		var sessionFactory = IMediaQuerySessionFactory.Mock();
		sessionFactory.CreateAsync(Any()).Returns(session.Object);

		var externalToId = builder.Items.Where(i => i.Resolves).ToDictionary(i => (IExternalMediaId)i.External, i => i.MediaId.Value);

		mediaRepo
			.QueryMediaIds(Any<IMediaQuerySession>(), Any<IEnumerable<IExternalMediaId>>())
			.Returns((_, externals) => externals.Where(externalToId.ContainsKey).Select(e => externalToId[e]).Distinct().AsQueryable());

		var realizedGuids = builder.Items.Where(i => i.Realizes).Select(i => i.MediaId.Value).ToHashSet();

		mediaRepo
			.QueryMediaIds(Any<IMediaQuerySession>(), Any<IEnumerable<MediaId>>())
			.Returns((_, ids) => ids.Select(id => id.Value).Where(realizedGuids.Contains).AsQueryable());

		mediaRepo.FindManyAsync(Any(), Any()).Returns((groups, _) => new MediaIdentity?[groups.Count]);
		mediaRepo.UpsertManyAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);

		var metadataMap = builder
			.Items.Where(i => i.Realizes)
			.ToDictionary(
				i => i.MediaId.Value,
				i => new CachedMedia(i.Detail is MovieDetail m ? m with { Identity = new MediaIdentity(i.MediaId, [i.External]) } : i.Detail, _now)
			);

		metaRepo
			.QueryMediaMetadataAsync(Any(), Any(), Any())
			.Returns((_, query, _) => (IReadOnlyList<CachedMedia>)query.ToList().Where(metadataMap.ContainsKey).Select(g => metadataMap[g]).ToList());

		foreach (var item in builder.Items.Where(i => !i.Resolves))
		{
			var key = MediaUnresolvedCacheKey.Build([item.External], MediaProvider.Tmdb);
			unresolvedCache.TryGetValue(Arg.Is<string>(k => k == key)).Returns(true).SetsOutValue(true);
		}

		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(_now);
		var search = IMediaSearchProvider.Mock();
		var enricher = IMetadataEnricher.Mock();
		var rating = IMediaRatingService.Mock();
		var configRepository = IProviderConfigRepository.Mock();
		configRepository
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = MediaRecommendationProvider.Tmdb,
					SearchProvider = MediaSearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
				}
			);

		var searchIndex = new FakeKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider>().Register(MediaSearchProvider.Plex, search.Object);
		var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IMetadataEnricher>()
			.Register(MediaProvider.Plex, enricher.Object)
			.Register(MediaProvider.Tmdb, enricher.Object);
		var ratingIndex = new FakeKeyedServiceIndex<RatingProvider, IMediaRatingService>().Register(RatingProvider.Tmdb, rating.Object);

		var metadataResolver = new MediaMetadataResolver(searchIndex, enricherIndex, ratingIndex, configRepository.Object);
		var coordinator = new MediaIdentityCoordinator(mediaRepo.Object);
		return new MediaService(
			new MediaIdResolver(null, unresolvedCache.Object),
			metadataResolver,
			coordinator,
			mediaRepo.Object,
			metaRepo.Object,
			sessionFactory.Object,
			dateTimeProvider.Object
		);
	}
}

internal static class OrderingAssertionExtensions
{
	public static List<int> ExtractTargetExternals(this IEnumerable<Result<Identification>> results) =>
		[.. results.Where(r => r is Identification).Select(r => int.Parse(((Identification)r.Value).TargetExternal.Value))];

	public static List<MediaId?> ExtractMediaIds(this IEnumerable<Result<IMediaDetail>> results) =>
		[.. results.Select(r => r is IMediaDetail d ? (MediaId?)d.Identity.Id : null)];
}

internal sealed record TestItem
{
	public required int Index { get; init; }
	public required IExternalMediaId External { get; init; }
	public required MediaId MediaId { get; init; }
	public required IMediaDetail Detail { get; init; }
	public bool Resolves { get; init; } = true;
	public bool Realizes { get; init; } = true;
	public ResolveRequest Request => new([External], MediaProvider.Tmdb, new None());

	public static TestItem Create(int index)
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		return new TestItem
		{
			Index = index,
			External = TmdbId.From(index.ToString(), MediaType.Movie),
			MediaId = mediaId,
			Detail = new MovieDetail
			{
				Identity = new MediaIdentity(mediaId, [PlexId.From($"plex-{index}", MediaType.Movie)]),
				Title = $"Item {index}",
				Tagline = "Tag",
				Summary = "Summary",
				PosterUrl = "https://example.com/p.jpg",
				BackdropUrl = "https://example.com/b.jpg",
				ReleaseDate = new DateOnly(2024, 1, 1),
				Genres = ["Action"],
				Runtime = TimeSpan.FromMinutes(120),
			},
		};
	}
}

internal sealed class RequestBuilder
{
	private readonly List<TestItem> _items;

	private RequestBuilder(int count)
	{
		_items = [.. Enumerable.Range(1, count).Select(TestItem.Create)];
	}

	public static RequestBuilder Requests(int count) => new(count);

	public IReadOnlyList<TestItem> Items => _items;

	public RequestBuilder FailResolveAt(params int[] indices)
	{
		var set = indices.ToHashSet();
		Mutate(item => item with { Resolves = !set.Contains(item.Index) });
		return this;
	}

	public RequestBuilder FailRealizeAt(params int[] indices)
	{
		var set = indices.ToHashSet();
		Mutate(item => item with { Realizes = !set.Contains(item.Index) });
		return this;
	}

	public IEnumerable<ResolveRequest> AsRequests() => _items.Select(i => i.Request);

	public IEnumerable<MediaId> AsMediaIds() => _items.Select(i => i.MediaId);

	public IReadOnlyList<MediaId?> ExpectedMediaIds => [.. _items.Select(i => i.Resolves && i.Realizes ? (MediaId?)i.MediaId : null)];

	private void Mutate(Func<TestItem, TestItem> map)
	{
		for (var i = 0; i < _items.Count; i++)
		{
			_items[i] = map(_items[i]);
		}
	}
}
