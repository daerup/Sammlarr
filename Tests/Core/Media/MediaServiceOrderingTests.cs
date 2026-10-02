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
using TUnit.Assertions.Enums;

namespace Core.Tests.Media;

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
		var batchSize = MediaService.RecommendedConcurrency * 3;
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
		var batchSize = MediaService.RecommendedConcurrency * 3;
		var builder = RequestBuilder.Requests(batchSize);
		var testee = CreateTestee(builder);

		var results = await testee.GetDetailsAsync(builder.AsMediaIds()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_PreservesInputOrder()
	{
		var builder = RequestBuilder.Requests(5);
		var testee = CreateTestee(builder);

		var results = await testee.IdentifyWithDetailsAsync(builder.AsRequests().ToAsyncEnumerable()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_PreservesOrder_WhenSomeIdentifyFail()
	{
		var builder = RequestBuilder.Requests(6).FailResolveAt(2, 4, 6);
		var testee = CreateTestee(builder);

		var results = await testee.IdentifyWithDetailsAsync(builder.AsRequests().ToAsyncEnumerable()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_PreservesOrder_WhenBatchExceedsConcurrencyLimit()
	{
		var batchSize = MediaService.RecommendedConcurrency * 3;
		var builder = RequestBuilder.Requests(batchSize);
		var testee = CreateTestee(builder);

		var results = await testee.IdentifyWithDetailsAsync(builder.AsRequests().ToAsyncEnumerable()).ToListAsync();

		await Assert.That(results.ExtractMediaIds()).IsEquivalentTo(builder.ExpectedMediaIds, CollectionOrdering.Matching);
	}

	private static IMediaService CreateTestee(RequestBuilder builder)
	{
		var mediaRepo = IMediaRepository.Mock();
		var unmatchedCache = ITypedMemoryCache<string, bool>.Mock();

		var externalToId = builder.Items.Where(i => i.Resolves).ToDictionary(i => (IExternalMediaId)i.External, i => i.MediaId.Value);

		mediaRepo.FindIdentitiesAsync(Any(), Any()).Returns((groups, _) => new MediaIdentity?[groups.Count]);
		mediaRepo.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);

		var metadataMap = builder
			.Items.Where(i => i.Realizes)
			.ToDictionary(i => i.MediaId.Value, i => new CachedMedia(i.Detail with { Identity = new MediaIdentity(i.MediaId, [i.External]) }, _now));

		mediaRepo.GetAsync(Any(), Any()).Returns((ids, _) => ids.Select(id => id.Value).Where(metadataMap.ContainsKey).Select(g => metadataMap[g]).ToList());

		mediaRepo
			.GetByExternalsAsync(Any(), Any())
			.Returns(
				(externals, _) =>
					externals.Where(externalToId.ContainsKey).Select(e => externalToId[e]).Distinct().Where(metadataMap.ContainsKey).Select(g => metadataMap[g]).ToList()
			);

		foreach (var item in builder.Items.Where(i => !i.Resolves))
		{
			var key = MediaUnmatchedCacheKey.Build([item.External], MediaProvider.Tmdb);
			unmatchedCache.TryGetValue(Arg.Is<string>(k => k == key)).Returns(true).SetsOutValue(true);
		}

		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(_now);
		var search = IMediaSearchProvider.Mock();
		var enricher = IMetadataEnricher.Mock();
		var configRepository = IProviderConfigService.Mock();
		configRepository
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

		var searchIndex = new FakeKeyedServiceIndex<SearchProvider, IMediaSearchProvider>().Register(SearchProvider.Plex, search.Object);
		var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IMetadataEnricher>()
			.Register(MediaProvider.Plex, enricher.Object)
			.Register(MediaProvider.Tmdb, enricher.Object);

		var metadataResolver = new MediaMetadataResolver(searchIndex, enricherIndex, configRepository.Object, TestCapacity.Open(), dateTimeProvider.Object);
		var coordinator = new MediaIdentityCoordinator(mediaRepo.Object);
		return new MediaService(
			new MediaIdResolver([], unmatchedCache.Object, TestCapacity.Open(), dateTimeProvider.Object),
			metadataResolver,
			coordinator,
			new MediaResolutionBufferFactory(mediaRepo.Object, NullLogger<MediaResolutionBuffer>.Instance),
			TestSinks.AddressingBy(MediaProvider.Tmdb, configRepository.Object),
			mediaRepo.Object,
			dateTimeProvider.Object,
			NullLogger<MediaService>.Instance
		);
	}
}

internal static class OrderingAssertionExtensions
{
	public static List<int> ExtractTargetExternals(this IEnumerable<Result<MediaIdentity>> results) =>
		[
			.. results
				.Where(r => r is Success<MediaIdentity>)
				.Select(r => r is Success<MediaIdentity>(var identity) ? int.Parse(identity.Externals.OfProvider(MediaProvider.Tmdb)!.Value) : 0),
		];

	public static List<MediaId?> ExtractMediaIds(this IEnumerable<Result<IdentifiedMedia>> results) =>
		[.. results.Select(r => r is Success<IdentifiedMedia>(var identified) && identified is IMedia d ? (MediaId?)d.Identity.Id : null)];

	public static List<MediaId?> ExtractMediaIds(this IEnumerable<MediaLookup> results) =>
		[.. results.Select(r => r is IdentifiedMedia identified && identified is IMedia d ? (MediaId?)d.Identity.Id : null)];
}

internal sealed record TestItem
{
	public required int Index { get; init; }
	public required IExternalMediaId External { get; init; }
	public required MediaId MediaId { get; init; }
	public required Movie Detail { get; init; }
	public bool Resolves { get; init; } = true;
	public bool Realizes { get; init; } = true;
	public ResolveRequest Request => new([External], new None());

	public static TestItem Create(int index)
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		return new TestItem
		{
			Index = index,
			External = TmdbId.From(index.ToString(), MediaType.Movie),
			MediaId = mediaId,
			Detail = new Movie
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
