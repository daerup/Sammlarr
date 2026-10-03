using Contracts;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Search;
using Contracts.Settings;
using Core.Media;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;
using TUnit.Assertions.Enums;

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
			.IdentifyAsync([new IdentifyRequest([first], new None()), new IdentifyRequest([second], new None()), new IdentifyRequest([third], new None())])
			.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(3);
		foreach (var result in results)
		{
			await Assert.That(result).IsSuccess();
		}
	}

	[Test]
	public async Task IdentifyAsync_IssuesOneBatchedQueryForTheStoredTitles()
	{
		var first = TmdbId.From("10", MediaType.Movie);
		var second = TmdbId.From("11", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any())
			.Returns([FreshCached(MediaId.From(Guid.NewGuid()), first), FreshCached(MediaId.From(Guid.NewGuid()), second)]);

		await testee.IdentifyAsync([new IdentifyRequest([first], new None()), new IdentifyRequest([second], new None())]).ToListAsync();

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
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([Stored(firstDetail), Stored(secondDetail)]);

		var results = await testee.GetDetailsAsync([firstId, secondId]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0]).IsOfUnionType<Result<MatchedMedia>, MatchedMedia>().And.IsEqualTo(firstDetail);
		await Assert.That(results[1]).IsOfUnionType<Result<MatchedMedia>, MatchedMedia>().And.IsEqualTo(secondDetail);
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenTheDetailsAreMissing_YieldsError()
	{
		var id = MediaId.From(Guid.NewGuid());
		var (testee, _) = CreateTestee();

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task GetDetailsAsync_ForListItems_MapsEachThroughItsItem_InOrder()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		MatchedMedia firstDetail = BuildDetail(firstId);
		MatchedMedia secondDetail = BuildDetail(secondId);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([Stored(secondDetail), Stored(firstDetail)]);
		WantlistItem[] items = [WantlistItemFor(firstId, 1), WantlistItemFor(secondId, 2)];

		var results = await testee.GetDetailsAsync(items).ToListAsync();

		await Assert
			.That(results)
			.IsEquivalentTo(
				[new WantlistedMedia(firstDetail, items[0].AddedAt, items[0].Provenance), new WantlistedMedia(secondDetail, items[1].AddedAt, items[1].Provenance)],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task GetDetailsAsync_ForListItems_LeavesOutItemsWhoseDetailsCannotBeLoaded()
	{
		var cachedId = MediaId.From(Guid.NewGuid());
		var missingId = MediaId.From(Guid.NewGuid());
		MatchedMedia cachedDetail = BuildDetail(cachedId);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([Stored(cachedDetail)]);

		var results = await testee.GetDetailsAsync([WantlistItemFor(missingId, 1), WantlistItemFor(cachedId, 2)]).ToListAsync();

		await Assert.That(results.Select(result => result.Media)).IsEquivalentTo([cachedDetail]);
	}

	[Test]
	public async Task GetDetailsAsync_ById_IssuesSingleJoinedReadCall()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([Stored(BuildDetail(firstId)), Stored(BuildDetail(secondId))]);

		await testee.GetDetailsAsync([firstId, secondId]).ToListAsync();

		deps.MediaRepo.GetAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_OnMisses_FlushesTheDetailsUpsertOnceAtTheEnd()
	{
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var thirdId = MediaId.From(Guid.NewGuid());
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([StaleCached(firstId), StaleCached(secondId), StaleCached(thirdId)]);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(CompleteMovie(ext)));

		await testee.GetDetailsAsync([firstId, secondId, thirdId]).ToListAsync();

		deps.MediaRepo.UpsertDetailsAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ByRequest_IdentifiesAndFetches_YieldingDetailsPerRequest()
	{
		var firstExternal = TmdbId.From("100", MediaType.Movie);
		var secondExternal = TmdbId.From("200", MediaType.Movie);
		var firstId = MediaId.From(Guid.NewGuid());
		var secondId = MediaId.From(Guid.NewGuid());
		var firstDetail = BuildDetail(firstId, firstExternal);
		var secondDetail = BuildDetail(secondId, secondExternal);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).Returns([Stored(firstDetail), Stored(secondDetail)]);

		var results = await testee
			.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([firstExternal], new None()), new([secondExternal], new None()) }.ToAsyncEnumerable())
			.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0]).IsOfUnionType<MediaMatchResult, MatchedMedia>().And.IsEqualTo(firstDetail);
		await Assert.That(results[1]).IsOfUnionType<MediaMatchResult, MatchedMedia>().And.IsEqualTo(secondDetail);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenAProviderFoundTitleCannotBeFetched_YieldsItUnmatched()
	{
		var (testee, deps) = CreateTestee();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("not found"));

		var results = await testee
			.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([TmdbId.From("404", MediaType.Movie)], new None()) }.ToAsyncEnumerable())
			.ToListAsync();

		await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenAStoredTitleWithoutDetailsCannotBeFetched_AnswersItsIdentityWithEmptyDetails()
	{
		var id = MediaId.From(Guid.NewGuid());
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([StaleCached(id)]);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Throws(new HttpRequestException("plex is down"));

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert
			.That(results.Single())
			.IsSuccess()
			.And.Multiple(async media => await Assert.That(media).IsEqualTo(new MatchedMedia(StaleCached(id).Identity, new MovieDetails())));
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenStaleDetailsCannotBeRefreshed_AnswersTheStaleDetails()
	{
		var id = MediaId.From(Guid.NewGuid());
		var detail = BuildDetail(id);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any())
			.Returns([new StoredMedia(detail.Identity, Some.Of(new CachedDetails(detail.Details, _now - TimeSpan.FromDays(8), MediaField.None)))]);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("not found"));

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert.That(results.Single()).IsSuccess().And.Multiple(async media => await Assert.That(media).IsEqualTo(detail));
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenStaleDetailsAreRefreshed_AnswersTheFetchedDetails()
	{
		var id = MediaId.From(Guid.NewGuid());
		var stale = BuildDetail(id);
		var (testee, deps) = CreateTestee();
		deps.MediaRepo.GetAsync(Any(), Any())
			.Returns([
				new StoredMedia(stale.Identity, Some.Of(new CachedDetails(stale.Details with { Title = "Stale" }, _now - TimeSpan.FromDays(8), MediaField.None))),
			]);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(CompleteMovie(ext)));

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert.That(results.Single()).IsSuccess().And.Multiple(async media => await Assert.That(media.Details.Title).IsEqualTo("T"));
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenIdentifyFails_YieldsUnmatchedWithoutFetching()
	{
		var external = TmdbId.From("999", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		deps.UnmatchedCache.TryGetValue(Any<string>()).Returns(true).SetsOutValue(true);

		var results = await testee.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([external], new None()) }.ToAsyncEnumerable()).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0]).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		deps.MediaRepo.UpsertDetailsAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenManyRequests_RampsBatchSizesFromTen()
	{
		var (testee, deps) = CreateTestee();
		var batches = RecordBatchSizes(deps);

		await testee.IdentifyWithDetailsAsync(BuildRequests(100)).ToListAsync();

		await Assert.That(batches).IsEquivalentTo([10, 20, 40, 30], CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenManyRequests_YieldsTheFirstResultBeforeResolvingEverything()
	{
		var (testee, deps) = CreateTestee();
		var batches = RecordBatchSizes(deps);

		await foreach (var _ in testee.IdentifyWithDetailsAsync(BuildRequests(500)))
		{
			break;
		}

		await Assert.That(batches.Sum()).IsEqualTo(10);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenRequestsSpanManyBatches_CapsTheRampAndNeverRestartsIt()
	{
		var (testee, deps) = CreateTestee();
		var batches = RecordBatchSizes(deps);

		await testee.IdentifyWithDetailsAsync(BuildRequests(600)).ToListAsync();

		await Assert.That(batches).IsEquivalentTo([10, 20, 40, 80, 160, 200, 90], CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenTheRequestStreamIsLazy_DoesNotDrainItBeforeYielding()
	{
		var (testee, deps) = CreateTestee();
		RecordBatchSizes(deps);
		var pulled = 0;

		await foreach (var _ in testee.IdentifyWithDetailsAsync(CountingRequests(4000, () => Interlocked.Increment(ref pulled))))
		{
			break;
		}

		await Assert.That(pulled).IsGreaterThanOrEqualTo(10).And.IsLessThan(2000);
	}

	private static async IAsyncEnumerable<IdentifyRequest> CountingRequests(int count, Action onPulled)
	{
		foreach (var i in Enumerable.Range(0, count))
		{
			onPulled();
			yield return new IdentifyRequest([TmdbId.From(i.ToString(), MediaType.Movie)], new None());
			await Task.Yield();
		}
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenRequestCountIsBelowTheFirstBatch_UsesASingleBatch()
	{
		var (testee, deps) = CreateTestee();
		var batches = RecordBatchSizes(deps);

		var results = await testee.IdentifyWithDetailsAsync(BuildRequests(3)).ToListAsync();

		await Assert.That(batches).IsEquivalentTo([3], CollectionOrdering.Matching);
		await Assert.That(results.Count).IsEqualTo(3);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenBatchesRamp_StillYieldsOneResultPerRequestInOrder()
	{
		var (testee, _) = CreateTestee();

		var results = await testee.IdentifyWithDetailsAsync(BuildRequests(75)).ToListAsync();

		var externals = results.Select(r => r.Externals.OfType<TmdbId>().Single().Value).ToList();
		await Assert.That(externals).IsEquivalentTo([.. Enumerable.Range(0, 75).Select(i => i.ToString())], CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenTwoBatchesShareAnExternalId_YieldsBothUnderOneIdentity()
	{
		var shared = TmdbId.From("shared", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(CompleteMovie(ext)));
		var requests = await BuildRequests(60).ToListAsync();
		requests[0] = new IdentifyRequest([shared], new None());
		requests[55] = new IdentifyRequest([shared], new None());

		var results = (await testee.IdentifyWithDetailsAsync(requests.ToAsyncEnumerable()).ToListAsync()).Matched().ToList();

		await Assert.That(results).Count().IsEqualTo(requests.Count);
		await Assert.That(results[0].Identity.Id).IsEqualTo(results[55].Identity.Id);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenALaterRequestBridgesTwoIdentities_PersistsOnlyTheSurvivingIdentity()
	{
		var first = TmdbId.From("bridge-a", MediaType.Movie);
		var second = ImdbId.From("tt7777777", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(CompleteMovie(ext)));
		var upserted = RecordUpsertedIdentities(deps);

		var requests = new List<IdentifyRequest> { new([first], new None()), new([second], new None()), new([first, second], new None()) };

		var results = (await testee.IdentifyWithDetailsAsync(requests.ToAsyncEnumerable()).ToListAsync()).Matched().ToList();

		await Assert.That(results).Count().IsEqualTo(requests.Count);
		var survivor = results[0].Identity.Id;
		await Assert.That(results[1].Identity.Id).IsEqualTo(survivor);
		await Assert.That(results[2].Identity.Id).IsEqualTo(survivor);
		await Assert.That(upserted.Select(identity => identity.Id).Distinct()).IsEquivalentTo([survivor]);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenALaterBatchBridgesTwoIdentities_PersistsOnlyTheSurvivingIdentity()
	{
		var first = TmdbId.From("batch-bridge-a", MediaType.Movie);
		var second = ImdbId.From("tt8888888", MediaType.Movie);
		var (testee, deps) = CreateTestee();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(CompleteMovie(ext)));
		var upserted = RecordUpsertedIdentities(deps);
		var requests = await BuildRequests(60).ToListAsync();
		requests[0] = new IdentifyRequest([first], new None());
		requests[1] = new IdentifyRequest([second], new None());
		requests[55] = new IdentifyRequest([first, second], new None());

		var results = (await testee.IdentifyWithDetailsAsync(requests.ToAsyncEnumerable()).ToListAsync()).Matched().ToList();

		await Assert.That(results).Count().IsEqualTo(requests.Count);
		var survivor = results[55].Identity.Id;
		await Assert.That(Bridged(upserted, first, second)).IsEquivalentTo([survivor]);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenALaterBatchMatchesAPersistedIdentity_KeepsThePersistedMediaId()
	{
		var minted = TmdbId.From("batch-persisted-a", MediaType.Movie);
		var known = ImdbId.From("tt6666666", MediaType.Movie);
		var persisted = new MediaIdentity(MediaId.From(Guid.NewGuid()), [known]);
		var (testee, deps) = CreateTestee();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(CompleteMovie(ext)));
		deps.MediaRepo.FindIdentitiesAsync(Any<IReadOnlyList<IReadOnlyList<IExternalMediaId>>>(), Any())
			.Returns((groups, _) => [.. groups.Select(group => group.Contains(known) ? persisted : (MediaIdentity?)null)]);
		var upserted = RecordUpsertedIdentities(deps);
		var requests = await BuildRequests(60).ToListAsync();
		requests[0] = new IdentifyRequest([minted], new None());
		requests[55] = new IdentifyRequest([minted, known], new None());

		var results = (await testee.IdentifyWithDetailsAsync(requests.ToAsyncEnumerable()).ToListAsync()).Matched().ToList();

		await Assert.That(results).Count().IsEqualTo(requests.Count);
		await Assert.That(results[55].Identity.Id).IsEqualTo(persisted.Id);
		await Assert.That(Bridged(upserted, minted, known)).IsEquivalentTo([persisted.Id]);
	}

	[Test]
	public async Task RegisterAsync_WhenEmpty_DoesNotWriteToRepository()
	{
		var (testee, deps) = CreateTestee();

		var identities = await testee.RegisterAsync([]);

		await Assert.That(identities).IsEmpty();
		deps.MediaRepo.UpsertIdentitiesAsync(Any(), Any()).WasNeverCalled();
	}

	private static (IMediaService Testee, Deps Deps) CreateTestee()
	{
		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(_now);
		var mediaRepo = IMediaRepository.Mock();
		var search = IMediaSearchProvider.Mock();
		var unmatchedCache = ITypedMemoryCache<string, bool>.Mock();

		mediaRepo.FindIdentitiesAsync(Any<IReadOnlyList<IReadOnlyList<IExternalMediaId>>>(), Any()).Returns((groups, _) => new MediaIdentity?[groups.Count]);
		mediaRepo.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
		mediaRepo.GetAsync(Any(), Any()).Returns(Array.Empty<StoredMedia>());
		mediaRepo.GetByExternalsAsync(Any(), Any()).Returns(Array.Empty<StoredMedia>());

		var enricher = IDetailsEnricher.Mock();
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

		var searchIndex = new FakeKeyedServiceIndex<SearchProvider, IMediaSearchProvider>().Register(SearchProvider.Plex, search.Object);
		var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IDetailsEnricher>()
			.Register(MediaProvider.Plex, enricher.Object)
			.Register(MediaProvider.Tmdb, enricher.Object);

		var converter = IExternalIdConverter.Mock();
		converter.Provider.Returns(MediaProvider.Tmdb);
		converter.Lookups.Returns([new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tmdb], Cost: 1)]);
		converter.LookUpAsync(Any(), Any()).Returns((request, _) => FoundByFind(request));

		var detailsFetcher = new DetailsFetcher(searchIndex, enricherIndex, configRepo.Object, TestCapacity.Open(), dateTimeProvider.Object);
		var coordinator = new MediaIdentityCoordinator(mediaRepo.Object);
		var testee = new MediaService(
			new MediaIdResolver([converter.Object], unmatchedCache.Object, TestCapacity.Open(), dateTimeProvider.Object),
			detailsFetcher,
			coordinator,
			new MediaWriteBufferFactory(mediaRepo.Object, NullLogger<MediaWriteBuffer>.Instance),
			TestSinks.AddressingBy(MediaProvider.Tmdb, configRepo.Object),
			mediaRepo.Object,
			dateTimeProvider.Object,
			NullLogger<MediaService>.Instance
		);
		return (testee, new Deps(mediaRepo, search, unmatchedCache));
	}

	private record Deps(Mock<IMediaRepository> MediaRepo, Mock<IMediaSearchProvider> Search, Mock<ITypedMemoryCache<string, bool>> UnmatchedCache);

	private static Result<IdLookupResult> FoundByFind(IdLookupRequest request)
	{
		return request switch
		{
			ByIdRequest(_, var id) => Success.Of(new IdLookupResult([TmdbId.From($"found-{id.Value}", id.Type)], new None())),
			ByHintsRequest => Error.From("The test converter offers no search."),
		};
	}

	private static List<MediaIdentity> RecordUpsertedIdentities(Deps deps)
	{
		var upserted = new List<MediaIdentity>();
		deps.MediaRepo.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any())
			.Returns(
				(identities, _) =>
				{
					upserted.AddRange(identities);
					return [.. identities];
				}
			);
		return upserted;
	}

	private static IEnumerable<MediaId> Bridged(IEnumerable<MediaIdentity> upserted, params IExternalMediaId[] externals) =>
		upserted.Where(identity => externals.Any(identity.Externals.Contains)).Select(identity => identity.Id).Distinct();

	private static List<int> RecordBatchSizes(Deps deps)
	{
		var batches = new List<int>();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any())
			.Returns(
				(ids, _) =>
				{
					batches.Add(ids.Count);
					return [];
				}
			);
		return batches;
	}

	private static IAsyncEnumerable<IdentifyRequest> BuildRequests(int count) =>
		Enumerable.Range(0, count).Select(i => new IdentifyRequest([TmdbId.From(i.ToString(), MediaType.Movie)], new None())).ToAsyncEnumerable();

	private static StoredMedia FreshCached(MediaId id, IExternalMediaId external)
	{
		return Stored(new MatchedMedia(new MediaIdentity(id, [external]), new MovieDetails()));
	}

	private static StoredMedia Stored(MatchedMedia media) => new(media.Identity, Some.Of(new CachedDetails(media.Details, _now, MediaField.None)));

	private static StoredMedia StaleCached(MediaId id) => new(new MediaIdentity(id, [PlexId.From($"plex-{id}", MediaType.Movie)]), new None());

	private static WantlistItem WantlistItemFor(MediaId id, int day)
	{
		return new(id, new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero), new ManualOrigin(ManualSource.Search));
	}

	private static MatchedMedia BuildDetail(MediaId id, IExternalMediaId? extra = null)
	{
		var plex = PlexId.From($"plex-{id}", MediaType.Movie);
		return new(
			new MediaIdentity(id, extra is null ? [plex] : [plex, extra]),
			new MovieDetails
			{
				Title = "Test",
				Tagline = "Tag",
				Summary = "Summary",
				PosterUrl = "https://example.com/p.jpg",
				BackdropUrl = "https://example.com/b.jpg",
				ReleaseDate = new DateOnly(2024, 1, 1),
				Genres = ["Action"],
				Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.5f, 100)]),
				Runtime = TimeSpan.FromMinutes(120),
			}
		);
	}

	private static FoundMedia CompleteMovie(IExternalMediaId external)
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
				Rating = RatingSummary.From([new MediaRating(MediaProvider.Tmdb, 7.5f, 100)]),
				Runtime = TimeSpan.FromMinutes(90),
			}
		);
	}
}
