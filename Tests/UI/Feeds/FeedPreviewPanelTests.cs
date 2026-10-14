using System.ComponentModel;
using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blocklist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Processing;
using Contracts.Wantlist;
using Domain.Blocklist;
using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Domain.Usage;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Observability.Usage;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor;
using UI.Shared.Services;
using UI.Tests.Media;

namespace UI.Tests.Feeds;

public class FeedPreviewPanelTests : BunitContext
{
	[Test]
	public async Task Preview_WhenEveryOutcomeIsPresent_TheBadgesAccountForEveryFetchedItem()
	{
		var cut = RenderPanel(new SpreadPlanner(), resolved: 5, unmatched: 2);

		await ApplyAsync(cut, new Append());
		cut.WaitForState(() => BadgeCounts(cut).Skip(1).Sum() == 7);

		var badges = BadgeCounts(cut);

		await Assert.That(badges.Skip(1).Sum()).IsEqualTo(badges[0]);
		await Assert.That(badges).IsEquivalentTo([7, 1, 0, 2, 1, 1, 2], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Preview_ForAMirrorFeed_MarksOwnedAtRiskRowsInTheGrid()
	{
		var cut = RenderPanel(new OwnedPlanner(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		await Assert.That(cut.FindAll(".preview-main-results .result-leaving")).Count().IsEqualTo(2);
		await Assert.That(cut.FindAll(".preview-main-results .search-result-row")).Count().IsEqualTo(5);
		await Assert.That(BadgeCounts(cut)[4]).IsEqualTo(2);
		await Assert.That(LeavingBadge(cut)).IsEqualTo(2);
	}

	[Test]
	public async Task Preview_WhenTheFeedAlreadyOwnsAQualifyingRow_ItIsNotCountedAsAnImport()
	{
		var cut = RenderPanel(new OwnedPlanner(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		var badges = BadgeCounts(cut);

		await Assert.That(badges[2]).IsEqualTo(2);
		await Assert.That(badges[1]).IsEqualTo(1);
		await Assert.That(badges.Skip(1).Sum()).IsEqualTo(badges[0]);
	}

	[Test]
	public async Task Preview_WhenOnItsWayOutIsToggled_TheGridShowsOnlyTheLeavingRows()
	{
		var cut = RenderPanel(new OwnedPlanner(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		await cut.Find(".leaving-badge-slot .preview-filter-badge").ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindAll(".preview-main-results .search-result-row")).Count().IsEqualTo(2);
		await Assert.That(cut.FindAll(".preview-main-results .result-leaving")).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Preview_WhenABucketFilterIsActive_TheNoLongerOnTheListSectionIsHidden()
	{
		var gone = MediaId.From(new Guid(99, 0, 0, new byte[8]));
		var cut = RenderPanel(new GonePlanner(gone), resolved: 5, unmatched: 0, gone: gone);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-gone-results .search-result-row").Count == 1);

		await cut.FindAll(".stat-badges-row .preview-filter-badge")[1].ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindAll(".preview-gone-results")).IsEmpty();
	}

	[Test]
	public async Task Preview_ForAGoneRow_TheWantlistAndBlocklistTogglesAreAvailable()
	{
		var gone = MediaId.From(new Guid(99, 0, 0, new byte[8]));
		var cut = RenderPanel(new GonePlanner(gone), resolved: 5, unmatched: 0, gone: gone);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-gone-results .search-result-row").Count == 1);

		await Assert.That(cut.FindAll(".preview-gone-results button[aria-label='Add to wantlist']")).Count().IsEqualTo(1);
		await Assert.That(cut.FindAll(".preview-gone-results button[aria-label='Add to blocklist']")).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Preview_ForAnAppendFeed_MarksNothingAtRiskEvenWhenOwnedRowsNoLongerQualify()
	{
		var cut = RenderPanel(new OwnedPlanner(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Append());
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		await Assert.That(cut.FindAll(".preview-gone-results")).IsEmpty();
		await Assert.That(cut.FindAll(".leaving-badge-slot")).IsEmpty();
	}

	[Test]
	public async Task Apply_WithASortAndALimitButNoFilter_PlansADraftCarryingThemAndNoFilter()
	{
		var planner = IFeedRunPlanner.Mock();
		planner.PlanAsync(Any(), Any()).Returns(new AppendPlan([], [], FeedDelta.None, Conclusiveness.Conclusive));
		var cut = RenderPanel(planner.Object, resolved: 0, unmatched: 0);
		var sort = new QuerySort { Property = nameof(MediaQueryRow.Rating), Direction = ListSortDirection.Descending };
		var limit = new QueryLimit { Count = 3 };

		await ApplyAsync(cut, new Append(), sort, limit);

		planner
			.PlanAsync(
				Is<FeedPlanTarget>(target =>
					target is DraftFeed(var feed, _) && feed.Sort.ToNullable() == sort && feed.Limit.ToNullable() == limit && feed.Filter.IsNone()
				),
				Any()
			)
			.WasCalled(Times.Once);
	}

	private static int LeavingBadge(IRenderedComponent<FeedPreviewPanel> cut) =>
		int.Parse(cut.Find(".leaving-badge-slot .preview-filter-badge span").TextContent);

	[Test]
	public async Task Apply_AsksTheProviderAsTheFeedEditorForTheDraftsFeed()
	{
		var (planner, provider) = Loading(FieldNeeds.None);
		UsageMark mark = default;
		provider
			.GetFeedItemsAsync(Any(), Any(), Any())
			.Returns(
				(_, _, _) =>
				{
					mark = UsageCauses.Current;
					return AsyncSequence.Of(new Listed<MediaMatchResult>(0, Detail(MediaId.New())));
				}
			);
		var cut = RenderPanel(planner.Object, resolved: 0, unmatched: 0, provider: provider.Object);

		await ApplyAsync(cut, new Append());

		await Assert.That(mark).IsEqualTo(new UsageMark(UsageCause.FeedEditor, Some.Of(cut.Instance.DraftFeedId)));
	}

	[Test]
	public async Task Apply_LoadsTheListWithTheDraftsNeeds()
	{
		var needs = new FieldNeeds(MediaField.Genres, MediaFields.AllRatings);
		var (planner, provider) = Loading(needs);
		var cut = RenderPanel(planner.Object, resolved: 0, unmatched: 0, provider: provider.Object);

		await ApplyAsync(cut, new Append());

		provider.GetFeedItemsAsync(Any(), needs, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Apply_WhenTheDraftNowRequiresAFieldTheLoadDidNotNeed_LoadsTheListAgain()
	{
		var (planner, provider) = Loading(FieldNeeds.None, new FieldNeeds(MediaField.Genres, MediaFields.None));
		var cut = RenderPanel(planner.Object, resolved: 0, unmatched: 0, provider: provider.Object);
		await ApplyAsync(cut, new Append());

		await ApplyAsync(cut, new Append(), new QuerySort { Property = nameof(MediaQueryRow.Genres), Direction = ListSortDirection.Ascending });

		provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task Apply_WhenTheNewSortReadsNoNewField_KeepsTheLoadedList()
	{
		var (planner, provider) = Loading(new FieldNeeds(MediaField.Genres, MediaFields.None));
		var cut = RenderPanel(planner.Object, resolved: 0, unmatched: 0, provider: provider.Object);
		await ApplyAsync(cut, new Append());

		await ApplyAsync(cut, new Append(), new QuerySort { Property = nameof(MediaQueryRow.Genres), Direction = ListSortDirection.Ascending });

		provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ApplyAsync_AfterTheVisitEnded_DoesNothing()
	{
		var (planner, provider) = Loading(FieldNeeds.None);
		var cut = RenderPanel(planner.Object, resolved: 0, unmatched: 0, provider: provider.Object);
		using var visit = new CancellationTokenSource();
		await visit.CancelAsync();

		await ApplyAsync(cut, new Append(), visit: visit.Token);

		provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task Retry_AfterTheVisitEnded_LoadsNothing()
	{
		var (planner, provider) = Loading(FieldNeeds.None, FieldNeeds.None);
		provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns((_, _, _) => throw new RemoteUnavailableException(RemoteService.Mdblist, sent: true));
		var cut = RenderPanel(planner.Object, resolved: 0, unmatched: 0, provider: provider.Object);
		using var visit = new CancellationTokenSource();
		await ApplyAsync(cut, new Append(), visit: visit.Token);
		cut.WaitForState(() => Retry(cut).Count > 0);
		await visit.CancelAsync();

		await Retry(cut)[0].ClickAsync(new MouseEventArgs());

		provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ApplyAsync_WhenTheVisitEndsMidLoad_CancelsTheStreamWithoutAToast()
	{
		var (planner, provider) = Loading(FieldNeeds.None);
		var streaming = CancellationToken.None;
		var loaded = new TaskCompletionSource();
		provider
			.GetFeedItemsAsync(Any(), Any(), Any())
			.Returns(
				(_, _, token) =>
				{
					streaming = token;
					return HeldItems.UntilAsync(loaded.Task, token);
				}
			);
		var cut = RenderPanel(planner.Object, resolved: 0, unmatched: 0, provider: provider.Object);
		using var visit = new CancellationTokenSource();
		var applying = ApplyAsync(cut, new Append(), visit: visit.Token);
		cut.WaitForAssertion(() => provider.GetFeedItemsAsync(Any(), Any(), Any()).WasCalled(Times.Once));

		await visit.CancelAsync();
		await applying.WaitAsync(TimeSpan.FromSeconds(10));

		await Assert.That(streaming.IsCancellationRequested).IsTrue();
		await Assert.That(Services.GetRequiredService<ToastService>().Toasts).IsEmpty();
	}

	private static List<IElement> Retry(IRenderedComponent<FeedPreviewPanel> cut)
	{
		return [.. cut.FindAll("button").Where(button => button.TextContent.Contains("Retry"))];
	}

	private static (Mock<IFeedRunPlanner> Planner, Mock<IFeedProvider> Provider) Loading(params FieldNeeds[] needs)
	{
		var planner = IFeedRunPlanner.Mock();
		planner.NeedsAsync(Any(), Any()).ReturnsSequentially(needs);
		planner.PlanAsync(Any(), Any()).Returns(new AppendPlan([], [], FeedDelta.None, Conclusiveness.NoItems));
		var provider = IFeedProvider.Mock();
		provider.GetFeedItemsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of(new Listed<MediaMatchResult>(0, Detail(MediaId.New()))));
		return (planner, provider);
	}

	private static Task ApplyAsync(
		IRenderedComponent<FeedPreviewPanel> cut,
		FeedStrategy strategy,
		QuerySort? sort = null,
		QueryLimit? limit = null,
		CancellationToken visit = default
	)
	{
		return cut.InvokeAsync(() =>
			cut.Instance.ApplyAsync(
				new FeedConfig
				{
					Name = "counted",
					Provider = FeedProvider.Mdblist,
					Identifier = "list",
					Strategy = strategy,
					Sort = sort,
					Limit = limit,
				},
				visit
			)
		);
	}

	private static List<int> BadgeCounts(IRenderedComponent<FeedPreviewPanel> cut) =>
		[.. cut.FindAll(".stat-badges-row .preview-filter-badge span").Select(badge => int.Parse(badge.TextContent))];

	private IRenderedComponent<FeedPreviewPanel> RenderPanel(
		IFeedRunPlanner planner,
		int resolved,
		int unmatched,
		MediaId? gone = null,
		IFeedProvider? provider = null
	)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(RatingStub.Context());
		Services.AddLogging();
		Services.AddSingleton(planner);
		var wantlist = IWantlistService.Mock();
		wantlist.GetAsync(Any()).Returns(new WantlistSnapshot([]));
		var blocklist = IBlocklistService.Mock();
		blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));
		Services.AddSingleton(wantlist.Object);
		Services.AddSingleton(blocklist.Object);
		Services.AddSingleton(ICurrentUser.Mock().Object);
		Services.AddSingleton(IUserScope.Mock().Object);
		var media = IMediaService.Mock();
		if (gone is { } id)
		{
			media.GetDetailsAsync(Any(), Any(), Any()).Returns(AsyncSequence.Of<Result<MatchedMedia>>(Success.Of(Detail(id))));
		}
		Services.AddSingleton(media.Object);
		Services.AddSingleton<IKeyedServiceIndex<FeedProvider, IFeedProvider>>(
			new FakeKeyedServiceIndex<FeedProvider, IFeedProvider>().Register(FeedProvider.Mdblist, provider ?? new StubFeedService(resolved, unmatched))
		);
		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;

		return Render<FeedPreviewPanel>(parameters =>
			parameters
				.Add(
					panel => panel.Config,
					new FeedConfig
					{
						Name = "counted",
						Provider = FeedProvider.Mdblist,
						Identifier = "list",
					}
				)
				.Add(panel => panel.DraftFeedId, FeedId.From(Guid.NewGuid()))
		);
	}

	private sealed class OwnedPlanner : IFeedRunPlanner
	{
		public Task<FieldNeeds> NeedsAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken = default) => Task.FromResult(FieldNeeds.None);

		public Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default)
		{
			var verdicts = Items(target).Matched().Select(Owned).ToList();

			return Task.FromResult<FeedPlanResult>(
				target is DraftFeed(var definition, _) && definition.Strategy is Mirror
					? new MirrorPlan(verdicts, [], FeedDelta.None, Conclusiveness.Conclusive, new Dictionary<MediaId, int>())
					: new AppendPlan(verdicts, [], FeedDelta.None, Conclusiveness.Conclusive)
			);
		}

		private static QueryVerdict<MatchedMedia> Owned(MatchedMedia item, int index)
		{
			return index switch
			{
				0 => Verdict(item, QueryOutcome.Culled, owned: true),
				1 => Verdict(item, QueryOutcome.Qualified, owned: true, inBlocklist: true),
				2 => Verdict(item, QueryOutcome.Culled, owned: true, inBlocklist: true),
				3 => Verdict(item, QueryOutcome.Qualified, owned: true),
				_ => Verdict(item, QueryOutcome.Qualified),
			};
		}

		private static QueryVerdict<MatchedMedia> Verdict(MatchedMedia item, QueryOutcome outcome, bool owned = false, bool inBlocklist = false)
		{
			return new(
				item,
				outcome,
				new Enrollment
				{
					InWantlist = owned,
					InBlocklist = inBlocklist,
					Owned = owned,
				},
				ConsumesSlot: true
			);
		}
	}

	private sealed class GonePlanner(MediaId gone) : IFeedRunPlanner
	{
		public Task<FieldNeeds> NeedsAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken = default) => Task.FromResult(FieldNeeds.None);

		public Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default)
		{
			var verdicts = Items(target)
				.Matched()
				.Select(item => new QueryVerdict<MatchedMedia>(
					item,
					QueryOutcome.Qualified,
					new Enrollment
					{
						InWantlist = false,
						InBlocklist = false,
						Owned = false,
					},
					ConsumesSlot: true
				))
				.ToList();

			return Task.FromResult<FeedPlanResult>(
				new MirrorPlan(verdicts, [], new FeedDelta([], [], [gone]), Conclusiveness.Conclusive, new Dictionary<MediaId, int> { [gone] = 3 })
			);
		}
	}

	private static RawFeedLoad Load(FeedPlanTarget target) => target is DraftFeed(_, var load) ? load : new RawFeedLoad([], FeedLoadState.Finished);

	private static IReadOnlyList<MediaMatchResult> Items(FeedPlanTarget target) => [.. Load(target).Items.Select(listed => listed.Item)];

	private static MatchedMedia Detail(MediaId id)
	{
		return new MatchedMedia(
			new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Gone",
				ReleaseDate = new DateOnly(2020, 1, 1),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(90),
				Ratings = MediaRatings.None,
			}
		);
	}

	private sealed class SpreadPlanner : IFeedRunPlanner
	{
		public Task<FieldNeeds> NeedsAsync(Domain.Feed.Feed feed, CancellationToken cancellationToken = default) => Task.FromResult(FieldNeeds.None);

		public Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default)
		{
			var items = Items(target);
			var unmatchedItems = items.Select(item => item is UnmatchedMedia unmatched ? unmatched : null).OfType<UnmatchedMedia>().ToList();
			var verdicts = items.Matched().Select(Spread).ToList();

			return Task.FromResult<FeedPlanResult>(new AppendPlan(verdicts, unmatchedItems, FeedDelta.None, Conclusiveness.Conclusive));
		}

		private static QueryVerdict<MatchedMedia> Spread(MatchedMedia item, int index)
		{
			return index switch
			{
				0 => new QueryVerdict<MatchedMedia>(item, QueryOutcome.Qualified, Enrolled(), true),
				1 => new QueryVerdict<MatchedMedia>(item, QueryOutcome.Qualified, Enrolled(inWantlist: true), true),
				2 => new QueryVerdict<MatchedMedia>(item, QueryOutcome.Culled, Enrolled(), false),
				3 => new QueryVerdict<MatchedMedia>(item, QueryOutcome.Truncated, Enrolled(), false),
				_ => new QueryVerdict<MatchedMedia>(item, QueryOutcome.Qualified, Enrolled(inBlocklist: true), true),
			};
		}

		private static Enrollment Enrolled(bool inWantlist = false, bool inBlocklist = false) =>
			new()
			{
				InWantlist = inWantlist,
				InBlocklist = inBlocklist,
				Owned = false,
			};
	}

	private sealed class StubFeedService(int resolved, int unmatched) : IFeedProvider
	{
		public Task<Result<string>> NormalizeAsync(string input, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<Result<string>>(Success.Of(input));
		}

		public async IAsyncEnumerable<Listed<MediaMatchResult>> GetFeedItemsAsync(
			string identifier,
			FieldNeeds needs,
			[EnumeratorCancellation] CancellationToken cancellationToken = default
		)
		{
			for (var i = 0; i < resolved; i++)
			{
				await Task.Yield();
				yield return new Listed<MediaMatchResult>(i, Movie(i));
			}

			for (var i = 0; i < unmatched; i++)
			{
				await Task.Yield();
				yield return new Listed<MediaMatchResult>(resolved + i, Unknown(i));
			}
		}

		private static MatchedMedia Movie(int index)
		{
			return new(
				new MediaIdentity(MediaId.New(), [TmdbId.From($"movie-{index}", MediaType.Movie)]),
				new MovieDetails
				{
					Title = $"Movie {index}",
					ReleaseDate = new DateOnly(2020, 1, 1),
					Genres = [],
					Runtime = TimeSpan.FromMinutes(90),
					Ratings = MediaRatings.None,
				}
			);
		}

		private static UnmatchedMedia Unknown(int index)
		{
			return new([TmdbId.From($"unmatched-{index}", MediaType.Movie)], new MovieDetails { Title = $"Unmatched {index}" });
		}
	}
}
