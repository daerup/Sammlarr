using System.Runtime.CompilerServices;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blacklist;
using Contracts.DI;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Processing;
using Contracts.Watchlist;
using Domain.Blacklist;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Processing;
using Domain.Providers;
using Domain.Watchlist;
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
		var cut = RenderPanel(resolved: 5, unmatched: 2);
		var config = new FeedConfig
		{
			Name = "counted",
			Provider = FeedProvider.Mdblist,
			Identifier = "list",
		};

		await cut.InvokeAsync(() => cut.Instance.ApplyAsync(config));
		cut.WaitForState(() => BadgeCounts(cut).Skip(1).Sum() == 7);

		var badges = BadgeCounts(cut);

		await Assert.That(badges.Skip(1).Sum()).IsEqualTo(badges[0]);
		await Assert.That(badges).IsEquivalentTo([7, 1, 2, 1, 1, 2], CollectionOrdering.Matching);
	}

	private static List<int> BadgeCounts(IRenderedComponent<FeedPreviewPanel> cut) =>
		[.. cut.FindAll(".stat-badges-row .preview-filter-badge span").Select(badge => int.Parse(badge.TextContent))];

	private IRenderedComponent<FeedPreviewPanel> RenderPanel(int resolved, int unmatched)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();
		Services.AddSingleton<IFeedRunEngine>(new SpreadRunEngine());
		var watchlist = IWatchlistService.Mock();
		watchlist.GetWatchlistIdsAsync(Any()).Returns(new HashSet<MediaId>());
		var blacklist = IBlacklistService.Mock();
		blacklist.GetBlacklistIdsAsync(Any()).Returns(new HashSet<MediaId>());
		Services.AddSingleton(watchlist.Object);
		Services.AddSingleton(blacklist.Object);
		Services.AddSingleton(IMediaService.Mock().Object);
		Services.AddSingleton<IKeyedServiceIndex<FeedProvider, IFeedService>>(
			new FakeKeyedServiceIndex<FeedProvider, IFeedService>().Register(FeedProvider.Mdblist, new StubFeedService(resolved, unmatched))
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

	private sealed class SpreadRunEngine : IFeedRunEngine
	{
		public Task<FeedRunSummary> DryRunAsync(
			Domain.Feed.Feed feed,
			IReadOnlyList<MediaLookup> items,
			FeedSource source,
			CancellationToken cancellationToken = default
		)
		{
			var unresolved = items.Where(item => item is UnmatchedMedia).ToList();
			var verdicts = items.Where(item => item is not UnmatchedMedia).Select(Spread).ToList();

			return Task.FromResult(new FeedRunSummary(verdicts, unresolved, FeedDelta.None, Conclusiveness.Conclusive));
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

	private sealed class StubFeedService(int resolved, int unmatched) : IFeedService
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
