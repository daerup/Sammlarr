using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Core.Tests.Media;

public class MediaServiceTests
{
	private static readonly DateTimeOffset _now = TestMediaService.Now;

	[Test]
	public async Task IdentifyAsync_YieldsOneResultPerInput()
	{
		var first = TmdbId.From("1", MediaType.Movie);
		var second = TmdbId.From("2", MediaType.Movie);
		var third = TmdbId.From("3", MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any())
			.Returns([FreshCached(MediaId.New(), first), FreshCached(MediaId.New(), second), FreshCached(MediaId.New(), third)]);

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
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).Returns([FreshCached(MediaId.New(), first), FreshCached(MediaId.New(), second)]);

		await testee.IdentifyAsync([new IdentifyRequest([first], new None()), new IdentifyRequest([second], new None())]).ToListAsync();

		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_YieldsOneResultPerInput()
	{
		var firstId = MediaId.New();
		var secondId = MediaId.New();
		var firstDetail = BuildDetail(firstId);
		var secondDetail = BuildDetail(secondId);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([Stored(firstDetail), Stored(secondDetail)]);

		var results = await testee.GetDetailsAsync([firstId, secondId]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0]).IsOfUnionType<Result<MatchedMedia>, MatchedMedia>().And.IsEqualTo(firstDetail);
		await Assert.That(results[1]).IsOfUnionType<Result<MatchedMedia>, MatchedMedia>().And.IsEqualTo(secondDetail);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_ForManyBatches_ReadsTheConnectedIdServicesOnce()
	{
		var (testee, deps) = TestMediaService.Create();

		await testee.IdentifyWithDetailsAsync(TestMediaService.Requests(600)).ToListAsync();

		deps.IdReadiness.ConnectedAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenCancelledWhileFetchingDetails_StopsWithoutSettlingTheTitle()
	{
		using var cancellation = new CancellationTokenSource();
		var lookup = new TaskCompletionSource<Result<FoundMedia>>();
		var fetching = new TaskCompletionSource();
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).ReturnsAsync(lookup.Task).Callback(() => fetching.TrySetResult());
		var results = new List<MediaMatchResult>();
		var requests = new List<IdentifyRequest> { new([TmdbId.From("550", MediaType.Movie)], new None()) };

		async Task ConsumeAsync()
		{
			await foreach (var result in testee.IdentifyWithDetailsAsync(requests.ToAsyncEnumerable(), cancellation.Token))
			{
				results.Add(result);
			}
		}

		var consume = ConsumeAsync();
		await fetching.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await cancellation.CancelAsync();
		lookup.SetCanceled(cancellation.Token);

		await Assert.That(async () => await consume.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
		await Assert.That(results).IsEmpty();
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenTheDetailsAreMissing_YieldsError()
	{
		var id = MediaId.New();
		var (testee, _) = TestMediaService.Create();

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task GetDetailsAsync_ForListItems_MapsEachThroughItsItem_InOrder()
	{
		var firstId = MediaId.New();
		var secondId = MediaId.New();
		MatchedMedia firstDetail = BuildDetail(firstId);
		MatchedMedia secondDetail = BuildDetail(secondId);
		var (testee, deps) = TestMediaService.Create();
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
		var cachedId = MediaId.New();
		var missingId = MediaId.New();
		MatchedMedia cachedDetail = BuildDetail(cachedId);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([Stored(cachedDetail)]);

		var results = await testee.GetDetailsAsync([WantlistItemFor(missingId, 1), WantlistItemFor(cachedId, 2)]).ToListAsync();

		await Assert.That(results.Select(result => result.Media)).IsEquivalentTo([cachedDetail]);
	}

	[Test]
	public async Task GetDetailsAsync_ById_IssuesSingleJoinedReadCall()
	{
		var firstId = MediaId.New();
		var secondId = MediaId.New();
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([Stored(BuildDetail(firstId)), Stored(BuildDetail(secondId))]);

		await testee.GetDetailsAsync([firstId, secondId]).ToListAsync();

		deps.MediaRepo.GetAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_OnMisses_FlushesTheDetailsUpsertOnceAtTheEnd()
	{
		var firstId = MediaId.New();
		var secondId = MediaId.New();
		var thirdId = MediaId.New();
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([StaleCached(firstId), StaleCached(secondId), StaleCached(thirdId)]);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(TestMediaService.CompleteMovie(ext)));

		await testee.GetDetailsAsync([firstId, secondId, thirdId]).ToListAsync();

		deps.MediaRepo.UpsertDetailsAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ByRequest_IdentifiesAndFetches_YieldingDetailsPerRequest()
	{
		var firstExternal = TmdbId.From("100", MediaType.Movie);
		var secondExternal = TmdbId.From("200", MediaType.Movie);
		var firstId = MediaId.New();
		var secondId = MediaId.New();
		var firstDetail = BuildDetail(firstId, firstExternal);
		var secondDetail = BuildDetail(secondId, secondExternal);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetByExternalsAsync(Any(), Any()).Returns([Stored(firstDetail), Stored(secondDetail)]);

		var results = await testee
			.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([firstExternal], new None()), new([secondExternal], new None()) }.ToAsyncEnumerable())
			.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0]).IsOfUnionType<MediaMatchResult, MatchedMedia>().And.IsEqualTo(firstDetail with { Known = true });
		await Assert.That(results[1]).IsOfUnionType<MediaMatchResult, MatchedMedia>().And.IsEqualTo(secondDetail with { Known = true });
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenNoProviderKnowsTheDetailsOfAFoundTitle_YieldsItUnmatched()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("not found"));

		var results = await testee
			.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([TmdbId.From("404", MediaType.Movie)], new None()) }.ToAsyncEnumerable())
			.ToListAsync();

		await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenTheDetailsProviderIsUnavailable_YieldsTheFoundTitleMatchedAndDescribedByItsHints()
	{
		var external = TmdbId.From("550", MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From(new RemoteUnavailableException(RemoteService.Plex)));
		var hints = new IdentifyHints
		{
			Title = "Fight Club",
			Year = 1999,
			MediaType = MediaType.Movie,
		};

		var results = await testee.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([external], Some.Of(hints)) }.ToAsyncEnumerable()).ToListAsync();

		var matched = await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		await Assert.That(matched!.Details).IsEqualTo(new MovieDetails { Title = "Fight Club", ReleaseDate = new DateOnly(1999, 1, 1) });
		await Assert.That(matched.Identity.Keys).Contains(external);
		await Assert.That(matched.Known).IsFalse();
		deps.Claims.ClaimAsync(Any(), Any()).WasCalled(Times.Once);
		deps.MediaRepo.UpsertDetailsAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenAStoredTitleWithoutDetailsCannotBeFetched_AnswersItsIdentityWithEmptyDetails()
	{
		var id = MediaId.New();
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([StaleCached(id)]);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Throws(new HttpRequestException("plex is down"));

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert
			.That(results.Single())
			.IsSuccess()
			.And.Multiple(async media => await Assert.That(media).IsEqualTo(new MatchedMedia(StaleCached(id).Identity, new MovieDetails())));
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenStaleDetailsCannotBeRefreshed_AnswersTheStaleDetails()
	{
		var id = MediaId.New();
		var detail = BuildDetail(id);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any())
			.Returns([new StoredMedia(detail.Identity, Some.Of(new CachedDetails(detail.Details, _now - TimeSpan.FromDays(15), _now - TimeSpan.FromDays(15), [])))]);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Error.From("not found"));

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert.That(results.Single()).IsSuccess().And.Multiple(async media => await Assert.That(media).IsEqualTo(detail));
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenStaleDetailsAreRefreshed_AnswersTheFetchedDetails()
	{
		var id = MediaId.New();
		var stale = BuildDetail(id);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any())
			.Returns([
				new StoredMedia(
					stale.Identity,
					Some.Of(new CachedDetails(stale.Details with { Title = "Stale" }, _now - TimeSpan.FromDays(15), _now - TimeSpan.FromDays(15), []))
				),
			]);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns((ext, _) => Success.Of(TestMediaService.CompleteMovie(ext)));

		var results = await testee.GetDetailsAsync([id]).ToListAsync();

		await Assert.That(results.Single()).IsSuccess().And.Multiple(async media => await Assert.That(media.Details.Title).IsEqualTo("T"));
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenIdentifyFails_YieldsUnmatchedWithoutFetching()
	{
		var external = TmdbId.From("999", MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		deps.UnmatchedCache.GetValueOrNone(Any<string>()).Returns(Some.Of(true));

		var results = await testee.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([external], new None()) }.ToAsyncEnumerable()).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0]).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		deps.MediaRepo.UpsertDetailsAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenAHintedRequestStaysUnmatched_DescribesItByTheHints()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.UnmatchedCache.GetValueOrNone(Any<string>()).Returns(Some.Of(true));
		var hints = new IdentifyHints
		{
			Title = "Fight Club",
			Year = 1999,
			MediaType = MediaType.Show,
		};

		var results = await testee
			.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([TmdbId.From("999", MediaType.Movie)], Some.Of(hints)) }.ToAsyncEnumerable())
			.ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched =>
				await Assert.That(unmatched.Details).IsEqualTo(new ShowDetails { Title = "Fight Club", ReleaseDate = new DateOnly(1999, 1, 1) })
			);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenTheLookupCouldNotRun_IsUnmatchedAsCouldNotLookUp()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Converter.LookUpAsync(Any(), Any()).Returns(Error.From(new RemoteUnavailableException(RemoteService.Tmdb)));

		var results = await testee
			.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([ImdbId.From("tt0137523", MediaType.Movie)], new None()) }.ToAsyncEnumerable())
			.ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched => await Assert.That(unmatched.Reason).IsEqualTo(UnmatchedReason.CouldNotLookUp));
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_ForACachedMiss_ReportsNotFoundEarlier()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.UnmatchedCache.GetValueOrNone(Any<string>()).Returns(Some.Of(true));

		var results = await testee
			.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([TmdbId.From("999", MediaType.Movie)], new None()) }.ToAsyncEnumerable())
			.ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched => await Assert.That(unmatched.Reason).IsEqualTo(UnmatchedReason.NotFoundEarlier));
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenAHintedRequestWithoutAYearStaysUnmatched_LeavesTheReleaseDateOpen()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.UnmatchedCache.GetValueOrNone(Any<string>()).Returns(Some.Of(true));
		var hints = new IdentifyHints
		{
			Title = "Fight Club",
			Year = 0,
			MediaType = MediaType.Movie,
		};

		var results = await testee
			.IdentifyWithDetailsAsync(new List<IdentifyRequest> { new([TmdbId.From("999", MediaType.Movie)], Some.Of(hints)) }.ToAsyncEnumerable())
			.ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched =>
				await Assert
					.That(unmatched)
					.IsEqualTo(new UnmatchedMedia([TmdbId.From("999", MediaType.Movie)], new MovieDetails { Title = "Fight Club" }, UnmatchedReason.NotFoundEarlier))
			);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenManyRequests_RampsBatchSizesFromTen()
	{
		var (testee, deps) = TestMediaService.Create();
		var batches = RecordBatchSizes(deps);

		await testee.IdentifyWithDetailsAsync(TestMediaService.Requests(100)).ToListAsync();

		await Assert.That(batches).IsEquivalentTo([10, 20, 40, 30], CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenRequestsSpanManyBatches_CapsTheRampAndNeverRestartsIt()
	{
		var (testee, deps) = TestMediaService.Create();
		var batches = RecordBatchSizes(deps);

		await testee.IdentifyWithDetailsAsync(TestMediaService.Requests(600)).ToListAsync();

		await Assert.That(batches).IsEquivalentTo([10, 20, 40, 80, 160, 200, 90], CollectionOrdering.Matching);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenRequestCountIsBelowTheFirstBatch_UsesASingleBatch()
	{
		var (testee, deps) = TestMediaService.Create();
		var batches = RecordBatchSizes(deps);

		var results = await testee.IdentifyWithDetailsAsync(TestMediaService.Requests(3)).ToListAsync();

		await Assert.That(batches).IsEquivalentTo([3], CollectionOrdering.Matching);
		await Assert.That(results.Count).IsEqualTo(3);
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_WhenBatchesRamp_StillYieldsOneResultPerRequestInOrder()
	{
		var (testee, _) = TestMediaService.Create();

		var results = await testee.IdentifyWithDetailsAsync(TestMediaService.Requests(75)).ToListAsync();

		var externals = results.Select(r => r.Externals.OfType<TmdbId>().Single().Value).ToList();
		await Assert.That(externals).IsEquivalentTo([.. Enumerable.Range(0, 75).Select(i => i.ToString())], CollectionOrdering.Matching);
	}

	[Test]
	public async Task RegisterAsync_WhenEmpty_DoesNotWriteToRepository()
	{
		var (testee, deps) = TestMediaService.Create();

		var identities = await testee.RegisterAsync([], []);

		await Assert.That(identities).IsEmpty();
		deps.Claims.ClaimAsync(Any(), Any()).WasNeverCalled();
	}

	private static List<int> RecordBatchSizes(TestMediaService.Deps deps)
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

	private static StoredMedia FreshCached(MediaId id, IIdentityKey external)
	{
		return Stored(new MatchedMedia(new MediaIdentity(id, [external]), new MovieDetails()));
	}

	private static StoredMedia Stored(MatchedMedia media) => new(media.Identity, Some.Of(new CachedDetails(media.Details, _now, _now, [])));

	private static StoredMedia StaleCached(MediaId id) => new(new MediaIdentity(id, [PlexId.From($"plex-{id}", MediaType.Movie)]), new None());

	private static WantlistItem WantlistItemFor(MediaId id, int day)
	{
		return new(id, new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero), new ManualOrigin(ManualSource.Search));
	}

	private static MatchedMedia BuildDetail(MediaId id, IIdentityKey? extra = null)
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
				Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(100))]),
				Runtime = TimeSpan.FromMinutes(120),
			}
		);
	}
}
