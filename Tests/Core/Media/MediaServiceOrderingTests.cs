using Contracts;
using Contracts.Media;
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
		var builder = RequestBuilder.Requests(6).FailIdentifyAt(2, 4, 6);
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
		var builder = RequestBuilder.Requests(6).FailDetailsAt(2, 4, 6);
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
		var builder = RequestBuilder.Requests(6).FailIdentifyAt(2, 4, 6);
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
		var unmatchedCache = TestUnmatchedCache.Empty();

		var externalToId = builder.Items.Where(i => i.Identifies).ToDictionary(i => i.External, i => i.MediaId.Value);

		mediaRepo.FindIdentitiesAsync(Any(), Any()).Returns((groups, _) => [.. Enumerable.Repeat<Optional<MediaIdentity>>(new None(), groups.Count)]);
		mediaRepo.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);

		var storedMap = builder
			.Items.Where(i => i.HasDetails)
			.ToDictionary(
				i => i.MediaId.Value,
				i => new StoredMedia(new MediaIdentity(i.MediaId, [i.External]), Some.Of(new CachedDetails(i.Detail.Details, _now, MediaField.None)))
			);

		mediaRepo.GetAsync(Any(), Any()).Returns((ids, _) => ids.Select(id => id.Value).Where(storedMap.ContainsKey).Select(g => storedMap[g]).ToList());

		mediaRepo
			.GetByExternalsAsync(Any(), Any())
			.Returns(
				(externals, _) =>
					externals.Where(externalToId.ContainsKey).Select(e => externalToId[e]).Distinct().Where(storedMap.ContainsKey).Select(g => storedMap[g]).ToList()
			);

		foreach (var item in builder.Items.Where(i => !i.Identifies))
		{
			var key = UnmatchedCacheKey.Build([item.External], MediaProvider.Tmdb);
			unmatchedCache.GetValueOrNone(Is<string>(k => k == key)).Returns(Some.Of(true));
		}

		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(_now);
		var search = IMediaSearchProvider.Mock();
		var enricher = IDetailsEnricher.Mock();
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
		var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IDetailsEnricher>()
			.Register(MediaProvider.Plex, enricher.Object)
			.Register(MediaProvider.Tmdb, enricher.Object);

		var detailsFetcher = new DetailsFetcher(searchIndex, enricherIndex, configRepository.Object, TestCapacity.Open(), dateTimeProvider.Object);
		var coordinator = new MediaIdentityCoordinator(mediaRepo.Object);
		return new MediaService(
			new MediaIdResolver([], unmatchedCache.Object, TestCapacity.Open(), dateTimeProvider.Object),
			detailsFetcher,
			coordinator,
			new MediaWriteBufferFactory(mediaRepo.Object, NullLogger<MediaWriteBuffer>.Instance),
			TestSinks.AddressingBy(MediaProvider.Tmdb, configRepository.Object),
			mediaRepo.Object,
			dateTimeProvider.Object,
			NullLogger<MediaService>.Instance
		);
	}
}

internal static class OrderingAssertionExtensions
{
	public static List<int> ExtractTargetExternals(this IEnumerable<Result<MediaIdentity>> results)
	{
		return
		[
			.. results.Select(r => r.ToOptional()).WhereSome().Select(identity => int.Parse(identity.Externals.First(id => id.Source == MediaProvider.Tmdb).Value)),
		];
	}

	public static List<MediaId?> ExtractMediaIds(this IEnumerable<Result<MatchedMedia>> results)
	{
		return [.. results.Select(r => r.Match(matched => (MediaId?)matched.Identity.Id, _ => null))];
	}

	public static List<MediaId?> ExtractMediaIds(this IEnumerable<MediaMatchResult> results)
	{
		return [.. results.Select(r => r is MatchedMedia matched ? (MediaId?)matched.Identity.Id : null)];
	}
}

internal sealed record TestItem
{
	public required int Index { get; init; }
	public required IExternalMediaId External { get; init; }
	public required MediaId MediaId { get; init; }
	public required MatchedMedia Detail { get; init; }
	public bool Identifies { get; init; } = true;
	public bool HasDetails { get; init; } = true;
	public IdentifyRequest Request => new([External], new None());

	public static TestItem Create(int index)
	{
		var mediaId = MediaId.New();
		return new TestItem
		{
			Index = index,
			External = TmdbId.From(index.ToString(), MediaType.Movie),
			MediaId = mediaId,
			Detail = new MatchedMedia(
				new MediaIdentity(mediaId, [PlexId.From($"plex-{index}", MediaType.Movie)]),
				new MovieDetails
				{
					Title = $"Item {index}",
					Tagline = "Tag",
					Summary = "Summary",
					PosterUrl = "https://example.com/p.jpg",
					BackdropUrl = "https://example.com/b.jpg",
					ReleaseDate = new DateOnly(2024, 1, 1),
					Genres = ["Action"],
					Rating = RatingSummary.From([new TmdbRating(7.5f, Some.Of(100))]),
					Runtime = TimeSpan.FromMinutes(120),
				}
			),
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

	public RequestBuilder FailIdentifyAt(params int[] indices)
	{
		var set = indices.ToHashSet();
		Mutate(item => item with { Identifies = !set.Contains(item.Index) });
		return this;
	}

	public RequestBuilder FailDetailsAt(params int[] indices)
	{
		var set = indices.ToHashSet();
		Mutate(item => item with { HasDetails = !set.Contains(item.Index) });
		return this;
	}

	public IEnumerable<IdentifyRequest> AsRequests() => _items.Select(i => i.Request);

	public IEnumerable<MediaId> AsMediaIds() => _items.Select(i => i.MediaId);

	public IReadOnlyList<MediaId?> ExpectedMediaIds => [.. _items.Select(i => i.Identifies && i.HasDetails ? (MediaId?)i.MediaId : null)];

	private void Mutate(Func<TestItem, TestItem> map)
	{
		for (var i = 0; i < _items.Count; i++)
		{
			_items[i] = map(_items[i]);
		}
	}
}
