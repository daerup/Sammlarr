using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blacklist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Processing;
using Contracts.Watchlist;
using Domain.Blacklist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Processing;
using Domain.Providers;
using Domain.Watchlist;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Feeds;

public class FeedPreviewPanelTests : BunitContext
{
	[Test]
	public async Task Preview_WhenEveryOutcomeIsPresent_TheBadgesAccountForEveryFetchedItem()
	{
		var cut = RenderPanel(new SpreadRunEngine(), resolved: 5, unmatched: 2);

		await ApplyAsync(cut, new Append());
		cut.WaitForState(() => BadgeCounts(cut).Skip(1).Sum() == 7);

		var badges = BadgeCounts(cut);

		await Assert.That(badges.Skip(1).Sum()).IsEqualTo(badges[0]);
		await Assert.That(badges).IsEquivalentTo([7, 1, 0, 2, 1, 1, 2], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Preview_ForAMirrorFeed_MarksOwnedAtRiskRowsInTheGrid()
	{
		var cut = RenderPanel(new OwnedRunEngine(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		await Assert.That(cut.FindAll(".preview-main-results .result-leaving")).HasCount(2);
		await Assert.That(cut.FindAll(".preview-main-results .search-result-row")).HasCount(5);
		await Assert.That(BadgeCounts(cut)[4]).IsEqualTo(2);
		await Assert.That(LeavingBadge(cut)).IsEqualTo(2);
	}

	[Test]
	public async Task Preview_WhenAnOwnedRowIsBlacklisted_ItStillLeavesOnceItStopsQualifying()
	{
		var cut = RenderPanel(new OwnedRunEngine(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		await Assert.That(cut.FindAll(".preview-main-results .result-leaving")).HasCount(2);
		await Assert.That(BadgeCounts(cut)[2]).IsEqualTo(2);
	}

	[Test]
	public async Task Preview_WhenTheFeedAlreadyOwnsAQualifyingRow_ItIsNotCountedAsAnImport()
	{
		var cut = RenderPanel(new OwnedRunEngine(), resolved: 5, unmatched: 0);

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
		var cut = RenderPanel(new OwnedRunEngine(), resolved: 5, unmatched: 0);

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
		var cut = RenderPanel(new GoneRunEngine(gone), resolved: 5, unmatched: 0, gone: gone);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-gone-results .search-result-row").Count == 1);

		await cut.FindAll(".stat-badges-row .preview-filter-badge")[1].ClickAsync(new MouseEventArgs());

		await Assert.That(cut.FindAll(".preview-gone-results")).IsEmpty();
	}

	[Test]
	public async Task Preview_ForAGoneRow_TheWatchlistAndBlacklistTogglesAreAvailable()
	{
		var gone = MediaId.From(new Guid(99, 0, 0, new byte[8]));
		var cut = RenderPanel(new GoneRunEngine(gone), resolved: 5, unmatched: 0, gone: gone);

		await ApplyAsync(cut, new Mirror(3));
		cut.WaitForState(() => cut.FindAll(".preview-gone-results .search-result-row").Count == 1);

		await Assert.That(cut.FindAll(".preview-gone-results .search-result-toggle-btn")).HasCount(2);
	}

	[Test]
	public async Task Preview_ForAnAppendFeed_MarksNothingAtRiskEvenWhenOwnedRowsNoLongerQualify()
	{
		var cut = RenderPanel(new OwnedRunEngine(), resolved: 5, unmatched: 0);

		await ApplyAsync(cut, new Append());
		cut.WaitForState(() => cut.FindAll(".preview-main-results .search-result-row").Count == 5);

		await Assert.That(cut.FindAll(".preview-gone-results")).IsEmpty();
		await Assert.That(cut.FindAll(".leaving-badge-slot")).IsEmpty();
	}

	private static int LeavingBadge(IRenderedComponent<FeedPreviewPanel> cut) =>
		int.Parse(cut.Find(".leaving-badge-slot .preview-filter-badge span").TextContent);

	private static IElement TotalBadge(IRenderedComponent<FeedPreviewPanel> cut) => cut.FindAll(".stat-badges-row .preview-filter-badge")[0];

	private static Task ApplyAsync(IRenderedComponent<FeedPreviewPanel> cut, FeedStrategy strategy) =>
		cut.InvokeAsync(() =>
			cut.Instance.ApplyAsync(
				new FeedConfig
				{
					Name = "counted",
					Provider = FeedProvider.Mdblist,
					Identifier = "list",
					Strategy = strategy,
				}
			)
		);

	private static List<int> BadgeCounts(IRenderedComponent<FeedPreviewPanel> cut) =>
		[.. cut.FindAll(".stat-badges-row .preview-filter-badge span").Select(badge => int.Parse(badge.TextContent))];

	private IRenderedComponent<FeedPreviewPanel> RenderPanel(IFeedRunEngine engine, int resolved, int unmatched, MediaId? gone = null)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		Services.AddSingleton(engine);
		var watchlist = IWatchlistService.Mock();
		watchlist.GetWatchlistIdsAsync(Any()).Returns(new HashSet<MediaId>());
		var blacklist = IBlacklistService.Mock();
		blacklist.GetBlacklistIdsAsync(Any()).Returns(new HashSet<MediaId>());
		Services.AddSingleton(watchlist.Object);
		Services.AddSingleton(blacklist.Object);
		Services.AddSingleton(ICurrentUser.Mock().Object);
		var media = IMediaService.Mock();
		if (gone is { } id)
		{
			media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(Detail(id))));
		}
		Services.AddSingleton(media.Object);
		Services.AddSingleton<IKeyedServiceIndex<FeedProvider, IFeedProvider>>(
			new FakeKeyedServiceIndex<FeedProvider, IFeedProvider>().Register(FeedProvider.Mdblist, new StubFeedService(resolved, unmatched))
		);
		Services.AddScoped<Notifier>();
		Services.AddScoped(_ => new MediaProviderBrandingService(string.Empty));
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

	private sealed class OwnedRunEngine : IFeedRunEngine
	{
		public Task<FeedRunResult> PreviewAsync(FeedId feedId, ICurrentUser user, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<FeedRunResult> RunAsync(FeedId feedId, FeedRunTrigger trigger, ICurrentUser user, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<FeedRunSummary> DryRunAsync(
			Domain.Feed.Feed feed,
			IReadOnlyList<MediaLookup> items,
			FeedSource source,
			ICurrentUser user,
			CancellationToken cancellationToken = default
		)
		{
			var verdicts = items.Select(Owned).ToList();

			return Task.FromResult(new FeedRunSummary(verdicts, [], FeedDelta.None, Conclusiveness.Conclusive, new Dictionary<MediaId, int>()));
		}

		private static QueryVerdict<MediaLookup> Owned(MediaLookup item, int index) =>
			index switch
			{
				0 => Verdict(item, QueryOutcome.Culled, owned: true),
				1 => Verdict(item, QueryOutcome.Qualified, owned: true, inBlacklist: true),
				2 => Verdict(item, QueryOutcome.Culled, owned: true, inBlacklist: true),
				3 => Verdict(item, QueryOutcome.Qualified, owned: true),
				_ => Verdict(item, QueryOutcome.Qualified),
			};

		private static QueryVerdict<MediaLookup> Verdict(MediaLookup item, QueryOutcome outcome, bool owned = false, bool inBlacklist = false) =>
			new(
				item,
				outcome,
				new Enrollment
				{
					InWatchlist = owned,
					InBlacklist = inBlacklist,
					Owned = owned,
				},
				ConsumesSlot: true
			);
	}

	private sealed class GoneRunEngine(MediaId gone) : IFeedRunEngine
	{
		public Task<FeedRunResult> PreviewAsync(FeedId feedId, ICurrentUser user, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<FeedRunResult> RunAsync(FeedId feedId, FeedRunTrigger trigger, ICurrentUser user, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<FeedRunSummary> DryRunAsync(
			Domain.Feed.Feed feed,
			IReadOnlyList<MediaLookup> items,
			FeedSource source,
			ICurrentUser user,
			CancellationToken cancellationToken = default
		)
		{
			var verdicts = items
				.Select(item => new QueryVerdict<MediaLookup>(
					item,
					QueryOutcome.Qualified,
					new Enrollment
					{
						InWatchlist = false,
						InBlacklist = false,
						Owned = false,
					},
					ConsumesSlot: true
				))
				.ToList();

			return Task.FromResult(
				new FeedRunSummary(verdicts, [], new FeedDelta([], [], [gone]), Conclusiveness.Conclusive, new Dictionary<MediaId, int> { [gone] = 0 })
			);
		}
	}

	private static async IAsyncEnumerable<T> Async<T>(params T[] items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}

	private static IdentifiedMedia Detail(MediaId id) =>
		new PartialMovie
		{
			Identity = new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			Title = "Gone",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(2020, 1, 1),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(90),
			Rating = RatingSummary.None,
		};

	private sealed class SpreadRunEngine : IFeedRunEngine
	{
		public Task<FeedRunResult> PreviewAsync(FeedId feedId, ICurrentUser user, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<FeedRunResult> RunAsync(FeedId feedId, FeedRunTrigger trigger, ICurrentUser user, CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<FeedRunSummary> DryRunAsync(
			Domain.Feed.Feed feed,
			IReadOnlyList<MediaLookup> items,
			FeedSource source,
			ICurrentUser user,
			CancellationToken cancellationToken = default
		)
		{
			var unresolved = items.Where(item => item is UnmatchedMedia).ToList();
			var verdicts = items.Where(item => item is not UnmatchedMedia).Select(Spread).ToList();

			return Task.FromResult(new FeedRunSummary(verdicts, unresolved, FeedDelta.None, Conclusiveness.Conclusive, new Dictionary<MediaId, int>()));
		}

		private static QueryVerdict<MediaLookup> Spread(MediaLookup item, int index) =>
			index switch
			{
				0 => new QueryVerdict<MediaLookup>(item, QueryOutcome.Qualified, Enrolled(), true),
				1 => new QueryVerdict<MediaLookup>(item, QueryOutcome.Qualified, Enrolled(inWatchlist: true), true),
				2 => new QueryVerdict<MediaLookup>(item, QueryOutcome.Culled, Enrolled(), false),
				3 => new QueryVerdict<MediaLookup>(item, QueryOutcome.Truncated, Enrolled(), false),
				_ => new QueryVerdict<MediaLookup>(item, QueryOutcome.Qualified, Enrolled(inBlacklist: true), true),
			};

		private static Enrollment Enrolled(bool inWatchlist = false, bool inBlacklist = false) =>
			new()
			{
				InWatchlist = inWatchlist,
				InBlacklist = inBlacklist,
				Owned = false,
			};
	}

	private sealed class StubFeedService(int resolved, int unmatched) : IFeedProvider
	{
		public async IAsyncEnumerable<MediaLookup> GetFeedItemsAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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

		private static MediaLookup Movie(int index) =>
			new PartialMovie
			{
				Identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), [TmdbId.From($"movie-{index}", MediaType.Movie)]),
				Title = $"Movie {index}",
				Tagline = string.Empty,
				Summary = string.Empty,
				PosterUrl = string.Empty,
				BackdropUrl = string.Empty,
				ReleaseDate = new DateOnly(2020, 1, 1),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(90),
				Rating = RatingSummary.None,
			}.ToMatch();

		private static MediaLookup Unknown(int index) =>
			new UnmatchedMedia { Externals = [TmdbId.From($"unmatched-{index}", MediaType.Movie)], Title = $"Unmatched {index}" };
	}
}
