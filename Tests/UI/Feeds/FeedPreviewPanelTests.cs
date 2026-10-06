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
using Domain.Wantlist;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
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
	public async Task Preview_WhenAnOwnedRowIsBlocklisted_ItStillLeavesOnceItStopsQualifying()
	{
		var cut = RenderPanel(new OwnedPlanner(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		await Assert.That(cut.FindAll(".preview-main-results .result-leaving")).Count().IsEqualTo(2);
		await Assert.That(BadgeCounts(cut)[2]).IsEqualTo(2);
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
	public async Task Preview_WhenOnItsWayOutIsToggled_TheTotalBadgeStopsReadingAsActive()
	{
		var cut = RenderPanel(new OwnedPlanner(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		await Assert.That(TotalBadge(cut).ClassList).Contains("is-active");

		await cut.Find(".leaving-badge-slot .preview-filter-badge").ClickAsync(new MouseEventArgs());

		await Assert.That(TotalBadge(cut).ClassList).DoesNotContain("is-active");
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

		await Assert.That(cut.FindAll(".preview-gone-results .search-result-toggle-btn")).Count().IsEqualTo(2);
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

	private static IElement TotalBadge(IRenderedComponent<FeedPreviewPanel> cut) => cut.FindAll(".stat-badges-row .preview-filter-badge")[0];

	private static Task ApplyAsync(IRenderedComponent<FeedPreviewPanel> cut, FeedStrategy strategy, QuerySort? sort = null, QueryLimit? limit = null)
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
				}
			)
		);
	}

	private static List<int> BadgeCounts(IRenderedComponent<FeedPreviewPanel> cut) =>
		[.. cut.FindAll(".stat-badges-row .preview-filter-badge span").Select(badge => int.Parse(badge.TextContent))];

	private IRenderedComponent<FeedPreviewPanel> RenderPanel(IFeedRunPlanner planner, int resolved, int unmatched, MediaId? gone = null)
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
		var media = IMediaService.Mock();
		if (gone is { } id)
		{
			media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<MatchedMedia>>(Success.Of(Detail(id))));
		}
		Services.AddSingleton(media.Object);
		Services.AddSingleton<IKeyedServiceIndex<FeedProvider, IFeedProvider>>(
			new FakeKeyedServiceIndex<FeedProvider, IFeedProvider>().Register(FeedProvider.Mdblist, new StubFeedService(resolved, unmatched))
		);
		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;

		return Render<FeedPreviewPanel>(parameters =>
			parameters.Add(
				panel => panel.Config,
				new FeedConfig
				{
					Name = "counted",
					Provider = FeedProvider.Mdblist,
					Identifier = "list",
				}
			)
		);
	}

	private sealed class OwnedPlanner : IFeedRunPlanner
	{
		public Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default)
		{
			var verdicts = Load(target).Items.Matched().Select(Owned).ToList();

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
		public Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default)
		{
			var verdicts = Load(target)
				.Items.Matched()
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

	private static async IAsyncEnumerable<T> Async<T>(params T[] items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}

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
		public Task<FeedPlanResult> PlanAsync(FeedPlanTarget target, CancellationToken cancellationToken = default)
		{
			var load = Load(target);
			var unmatchedItems = load.Items.Select(item => item is UnmatchedMedia unmatched ? unmatched : null).OfType<UnmatchedMedia>().ToList();
			var verdicts = load.Items.Matched().Select(Spread).ToList();

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

		public async IAsyncEnumerable<MediaMatchResult> GetFeedItemsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			for (var i = 0; i < resolved; i++)
			{
				await Task.Yield();
				yield return Movie(i);
			}

			for (var i = 0; i < unmatched; i++)
			{
				await Task.Yield();
				yield return Unknown(i);
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
