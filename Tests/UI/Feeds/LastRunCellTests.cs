using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Contracts.Blacklist;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Watchlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Components;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Feeds;

public class LastRunCellTests : BunitContext
{
	private static readonly FeedId Feed = FeedId.From(new Guid(11, 0, 0, new byte[8]));
	private static readonly MediaId Added = MediaId.From(new Guid(12, 0, 0, new byte[8]));

	[Test]
	public async Task Dialog_WhenARowIsClicked_OpensTheDetailSheetForThatTitle()
	{
		RegisterServices();

		var cut = Render<Harness>();
		cut.WaitForState(() => cut.FindAll(".last-run").Count == 1);
		await cut.Find(".last-run").ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".change-row").Count == 1);
		await cut.Find(".change-row").ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".media-detail-sheet-content").Count == 1);
		await Assert.That(cut.Find(".media-detail-sheet-content").TextContent).Contains("Futurama");
	}

	private void RegisterServices()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(Detail(Added))));
		Services.AddSingleton(media.Object);

		var watchlist = IWatchlistService.Mock();
		watchlist.GetWatchlistIdsAsync(Any()).Returns(new HashSet<MediaId>());
		Services.AddSingleton(watchlist.Object);

		var blacklist = IBlacklistService.Mock();
		blacklist.GetBlacklistIdsAsync(Any()).Returns(new HashSet<MediaId>());
		Services.AddSingleton(blacklist.Object);

		var providerConfig = IProviderConfigService.Mock();
		providerConfig
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);
		Services.AddSingleton(providerConfig.Object);

		Services.AddScoped<Notifier>();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private static FeedRun Run() =>
		new()
		{
			Id = Guid.NewGuid(),
			FeedId = Feed,
			StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
			Duration = TimeSpan.FromSeconds(2),
			Strategy = new FeedStrategy(new Append()),
			Trigger = new FeedRunTrigger(new Manual()),
			Conclusiveness = Conclusiveness.Conclusive,
			Counts = new FeedRunCounts
			{
				Total = 1,
				Added = 1,
				Kept = 0,
				Bypassed = 0,
				Filtered = 0,
				Skipped = 0,
				Unresolved = 0,
				Removed = 0,
			},
			Items = [new FeedRunItem { MediaId = Added, State = FeedRunItemState.Added }],
		};

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
			Title = "Futurama",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = new DateOnly(1999, 3, 28),
			Genres = [],
			Runtime = TimeSpan.FromMinutes(22),
			Rating = RatingSummary.None,
		};

	private sealed class Harness : ComponentBase
	{
		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<LastRunCell>(1);
			builder.AddComponentParameter(2, nameof(LastRunCell.FeedId), Feed);
			builder.AddComponentParameter(
				3,
				nameof(LastRunCell.Overview),
				new FeedOverview(
					Run(),
					new FeedRunHistory
					{
						TotalRuns = 1,
						FirstRunAt = Run().StartedAt,
						TotalAdded = 1,
						TotalRemoved = 0,
					}
				)
			);
			builder.CloseComponent();
		}
	}
}
