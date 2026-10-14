using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using Shared.Concurrency;
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

		var results = await testee.GetDetailsAsync([firstId, secondId], FieldNeeds.Everything).ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0]).IsOfUnionType<Result<MatchedMedia>, MatchedMedia>().And.IsEqualTo(firstDetail);
		await Assert.That(results[1]).IsOfUnionType<Result<MatchedMedia>, MatchedMedia>().And.IsEqualTo(secondDetail);
	}

	[Test]
	public async Task LoadListAsync_WhenCancelledWhileFetchingDetails_StopsWithoutHandingOutTheTitle()
	{
		using var cancellation = new CancellationTokenSource();
		var lookup = new TaskCompletionSource<Answer<FoundMedia>>();
		var fetching = new TaskCompletionSource();
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).ReturnsAsync(lookup.Task).Callback(() => fetching.TrySetResult());
		var results = new List<MediaMatchResult>();
		var requests = new List<IdentifyRequest> { new([TmdbId.From("550", MediaType.Movie)], new None()) };

		async Task ConsumeAsync()
		{
			await foreach (
				var result in testee
					.LoadListAsync(requests.ToAsyncEnumerable().AsListTitles(), FieldNeeds.Everything, cancellation.Token)
					.InListOrderAsync(cancellation.Token)
			)
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
	public async Task LoadListAsync_ForATitle_AsksItsProvidersAtTitlePriority()
	{
		var asked = new List<LaneClass>();
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any())
			.Returns(
				(ext, _, _) =>
				{
					asked.Add(LanePriority.Current.Class);
					return new Found<FoundMedia>(TestMediaService.CompleteMovie(ext));
				}
			);

		await testee
			.LoadListAsync(new[] { new IdentifyRequest([TmdbId.From("550", MediaType.Movie)], new None()) }.ToAsyncEnumerable().AsListTitles(), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert.That(asked).IsEquivalentTo([LaneClass.Title]);
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenTheDetailsAreMissing_YieldsError()
	{
		var id = MediaId.New();
		var (testee, _) = TestMediaService.Create();

		var results = await testee.GetDetailsAsync([id], FieldNeeds.Everything).ToListAsync();

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

		var results = await testee.GetDetailsAsync(items, FieldNeeds.Everything).ToListAsync();

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

		var results = await testee.GetDetailsAsync([WantlistItemFor(missingId, 1), WantlistItemFor(cachedId, 2)], FieldNeeds.Everything).ToListAsync();

		await Assert.That(results.Select(result => result.Media)).IsEquivalentTo([cachedDetail]);
	}

	[Test]
	public async Task GetDetailsAsync_ById_IssuesSingleJoinedReadCall()
	{
		var firstId = MediaId.New();
		var secondId = MediaId.New();
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([Stored(BuildDetail(firstId)), Stored(BuildDetail(secondId))]);

		await testee.GetDetailsAsync([firstId, secondId], FieldNeeds.Everything).ToListAsync();

		deps.MediaRepo.GetAsync(Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetDetailsAsync_ById_OnMisses_SavesEachFetchedTitle()
	{
		var firstId = MediaId.New();
		var secondId = MediaId.New();
		var thirdId = MediaId.New();
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([StaleCached(firstId), StaleCached(secondId), StaleCached(thirdId)]);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns((ext, _, _) => new Found<FoundMedia>(TestMediaService.CompleteMovie(ext)));

		await testee.GetDetailsAsync([firstId, secondId, thirdId], FieldNeeds.Everything).ToListAsync();

		var saved = Mock.Invocations(deps.MediaRepo).Select(call => call.Arguments[0]).OfType<IReadOnlyList<DetailsChange>>().SelectMany(changes => changes);
		await Assert.That(saved.Select(change => change.Id)).IsEquivalentTo([firstId, secondId, thirdId]);
	}

	[Test]
	public async Task LoadListAsync_ForTwoStoredTitles_YieldsEachKnownWithItsStoredDetails()
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
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([firstExternal], new None()), new([secondExternal], new None())), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert.That(results.Count).IsEqualTo(2);
		await Assert.That(results[0]).IsOfUnionType<MediaMatchResult, MatchedMedia>().And.IsEqualTo(firstDetail with { Known = true });
		await Assert.That(results[1]).IsOfUnionType<MediaMatchResult, MatchedMedia>().And.IsEqualTo(secondDetail with { Known = true });
	}

	[Test]
	public async Task LoadListAsync_WhenNoProviderKnowsTheDetailsOfAFoundTitle_YieldsItUnmatched()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new NoMatch(RemoteService.Plex));

		var results = await testee
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([TmdbId.From("404", MediaType.Movie)], new None())), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadListAsync_WhenTheDetailsProviderIsUnavailable_YieldsTheFoundTitleMatchedAndDescribedByItsHints()
	{
		var external = TmdbId.From("550", MediaType.Movie);
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Unavailable(RemoteService.Plex, new None(), true));
		var hints = new IdentifyHints
		{
			Title = "Fight Club",
			Year = 1999,
			MediaType = MediaType.Movie,
		};

		var results = await testee
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([external], Some.Of(hints))), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		var matched = await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		await Assert.That(matched!.Details).IsEqualTo(new MovieDetails { Title = "Fight Club", ReleaseDate = new DateOnly(1999, 1, 1) });
		await Assert.That(matched.Identity.Keys).Contains(external);
		await Assert.That(matched.Known).IsFalse();
		deps.Claims.ClaimAsync(Any(), Any()).WasCalled(Times.Once);
		deps.MediaRepo.SaveDetailsAsync(Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenAStoredTitleWithoutDetailsCannotBeFetched_AnswersItsIdentityWithEmptyDetails()
	{
		var id = MediaId.New();
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any()).Returns([StaleCached(id)]);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new Unavailable(RemoteService.Plex, new None(), true));

		var results = await testee.GetDetailsAsync([id], FieldNeeds.Everything).ToListAsync();

		await Assert
			.That(results.Single())
			.IsSuccess()
			.And.Multiple(async media => await Assert.That(media).IsEqualTo(new MatchedMedia(StaleCached(id).Identity, new MovieDetails())));
	}

	[Test]
	public async Task GetDetailsAsync_ById_WhenNoServiceFindsAStaleTitle_AnswersTheStaleDetailsUnmarked()
	{
		var id = MediaId.New();
		var detail = BuildDetail(id);
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any())
			.Returns([new StoredMedia(detail.Identity, Some.Of(new CachedDetails(detail.Details, _now - TimeSpan.FromDays(15), _now - TimeSpan.FromDays(15), [])))]);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns(new NoMatch(RemoteService.Plex));

		var results = await testee.GetDetailsAsync([id], FieldNeeds.Everything).ToListAsync();

		await Assert.That(results.Single()).IsSuccess().And.Multiple(async media => await Assert.That(media).IsEqualTo(detail));
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasCalled(Times.Once);
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
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns((ext, _, _) => new Found<FoundMedia>(TestMediaService.CompleteMovie(ext)));

		var results = await testee.GetDetailsAsync([id], FieldNeeds.Everything).ToListAsync();

		await Assert.That(results.Single()).IsSuccess().And.Multiple(async media => await Assert.That(media.Details.Title).IsEqualTo("T"));
	}

	[Test]
	public async Task LoadListAsync_WhenAHintedRequestStaysUnmatched_DescribesItByTheHints()
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
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([TmdbId.From("999", MediaType.Movie)], Some.Of(hints))), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched =>
				await Assert.That(unmatched.Details).IsEqualTo(new ShowDetails { Title = "Fight Club", ReleaseDate = new DateOnly(1999, 1, 1) })
			);
	}

	[Test]
	public async Task LoadListAsync_WhenFetchingTheDetailsOfANewTitleThrows_IsUnmatchedAsCouldNotLookUp()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Throws(new InvalidOperationException("The library is locked."));

		var results = await testee
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([TmdbId.From("550", MediaType.Movie)], new None())), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched => await Assert.That(unmatched.Reason).IsEqualTo(UnmatchedReason.CouldNotLookUp));
	}

	[Test]
	public async Task LoadListAsync_ForATitleAConverterResolved_ClaimsItWithThatConverterAsASource()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).Returns((ext, _, _) => new Found<FoundMedia>(TestMediaService.CompleteMovie(ext)));

		await testee
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([ImdbId.From("tt0137523", MediaType.Movie)], new None())), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		deps.Claims.ClaimAsync(Is<IdentityClaim>(claim => claim.Sources.Contains(RemoteService.Tmdb)), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task LoadListAsync_WhenTheLookupCouldNotRun_IsUnmatchedAsCouldNotLookUp()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.Converter.LookUpAsync(Any(), Any()).Returns(new Unavailable(RemoteService.Tmdb, new None(), true));

		var results = await testee
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([ImdbId.From("tt0137523", MediaType.Movie)], new None())), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched => await Assert.That(unmatched.Reason).IsEqualTo(UnmatchedReason.CouldNotLookUp));
	}

	[Test]
	public async Task LoadListAsync_ForACachedMiss_ReportsNotFoundEarlier()
	{
		var (testee, deps) = TestMediaService.Create();
		deps.UnmatchedCache.GetValueOrNone(Any<string>()).Returns(Some.Of(true));

		var results = await testee
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([TmdbId.From("999", MediaType.Movie)], new None())), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert
			.That(results.Single())
			.IsOfUnionType<MediaMatchResult, UnmatchedMedia>()
			.And.Multiple(async unmatched => await Assert.That(unmatched.Reason).IsEqualTo(UnmatchedReason.NotFoundEarlier));
	}

	[Test]
	public async Task LoadListAsync_WhenAHintedRequestWithoutAYearStaysUnmatched_LeavesTheReleaseDateOpen()
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
			.LoadListAsync(ListTitles.Of(new IdentifyRequest([TmdbId.From("999", MediaType.Movie)], Some.Of(hints))), FieldNeeds.Everything)
			.InListOrderAsync()
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
	public async Task LoadListAsync_ForAPagedList_NumbersEveryEntryAcrossPages()
	{
		var (testee, _) = TestMediaService.Create();

		var results = await testee.LoadListAsync(TestMediaService.Paged(40, 35).AsListTitles(), FieldNeeds.Everything).ToListAsync();

		var numbered = results.OrderBy(result => result.Position).Select(result => (result.Position, result.Item.Externals.OfType<TmdbId>().Single().Value));
		await Assert.That(numbered).IsEquivalentTo([.. Enumerable.Range(0, 75).Select(i => (i, i.ToString()))], CollectionOrdering.Matching);
	}

	[Test]
	public async Task GetStoredDetailsAsync_ForAStaleTitleAndOneWithoutDetails_AnswersWhatIsStoredWithoutAskingAnyService()
	{
		var stale = BuildDetail(MediaId.New());
		var bare = StaleCached(MediaId.New());
		var (testee, deps) = TestMediaService.Create();
		deps.MediaRepo.GetAsync(Any(), Any())
			.Returns([
				new StoredMedia(stale.Identity, Some.Of(new CachedDetails(stale.Details, _now - TimeSpan.FromDays(15), _now - TimeSpan.FromDays(15), []))),
				bare,
			]);

		var stored = await testee.GetStoredDetailsAsync([stale.Identity.Id, bare.Identity.Id]);

		await Assert.That(stored.Keys).IsEquivalentTo([stale.Identity.Id]);
		await Assert.That(stored[stale.Identity.Id]).IsEqualTo(stale.Details);
		deps.Details.LookUpAsync(Any<IExternalMediaId>(), Any(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task RegisterAsync_WhenEmpty_DoesNotWriteToRepository()
	{
		var (testee, deps) = TestMediaService.Create();

		var identities = await testee.RegisterAsync([], []);

		await Assert.That(identities).IsEmpty();
		deps.Claims.ClaimAsync(Any(), Any()).WasNeverCalled();
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
