using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Services;
using Bunit;
using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Blocklist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
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

	[Test]
	public async Task Dialog_WhenOpenedAndClosed_TellsItsHostBothTimes()
	{
		RegisterServices();

		var cut = Render<Harness>();
		cut.WaitForState(() => cut.FindAll(".last-run").Count == 1);
		await cut.Find(".last-run").ClickAsync(new());
		cut.WaitForState(() => cut.Instance.Opened.Count == 1);

		await cut.FindAll("[role=dialog] button").Last(button => button.TextContent.Contains("Close")).ClickAsync(new());
		cut.WaitForState(() => cut.Instance.Opened.Count == 2);

		await Assert.That(cut.Instance.Opened).IsEquivalentTo([true, false], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Dialog_ForARunWhoseSourceFailed_SaysItFailedAndWhy()
	{
		RegisterServices();
		var failed = Run() with
		{
			Conclusiveness = Conclusiveness.StillLoading,
			Counts = FeedRunCounts.None,
			Items = [],
			SourceFailure = "No list found with identifier 'hdlists/comedy'.",
		};

		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.LastRun, failed));
		cut.WaitForState(() => cut.FindAll(".last-run").Count == 1);
		await cut.Find(".last-run").ClickAsync(new());
		cut.WaitForState(() => cut.FindAll(".change-band").Count == 1);

		await Assert.That(cut.Find(".change-badges").TextContent).Contains("Failed");
		await Assert.That(cut.Find(".change-band").TextContent).Contains("No list found with identifier 'hdlists/comedy'.");
	}

	private void RegisterServices()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(Async<Result<IdentifiedMedia>>(Success.Of(Detail(Added))));
		Services.AddSingleton(media.Object);

		var wantlist = IWantlistService.Mock();
		wantlist.GetAsync(Any()).Returns(new WantlistSnapshot([]));
		Services.AddSingleton(wantlist.Object);

		var blocklist = IBlocklistService.Mock();
		blocklist.GetAsync(Any()).Returns(new BlocklistSnapshot([]));
		Services.AddSingleton(blocklist.Object);

		var providerConfig = IProviderConfigService.Mock();
		providerConfig
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
		Services.AddSingleton(providerConfig.Object);

		Services.AddScoped<Notifier>();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	private static FeedRun Run() =>
		new()
		{
			Id = RunId.From(Guid.NewGuid()),
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
				Unmatched = 0,
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

	private static IdentifiedMedia Detail(MediaId id)
	{
		return new IdentifiedMedia(
			new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Futurama",
				ReleaseDate = new DateOnly(1999, 3, 28),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(22),
				Rating = RatingSummary.None,
			}
		);
	}

	private sealed class Harness : ComponentBase
	{
		internal List<bool> Opened { get; } = [];

		[Parameter]
		public FeedRun LastRun { get; set; } = Run();

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<LastRunCell>(1);
			builder.AddComponentParameter(
				2,
				nameof(LastRunCell.Overview),
				new FeedOverview(
					TestFeeds.Feed(Feed.Value),
					Some.Of(LastRun),
					new FeedRunHistory
					{
						TotalRuns = 1,
						FirstRunAt = LastRun.StartedAt,
						TotalAdded = 1,
						TotalRemoved = 0,
					},
					0
				)
			);
			builder.AddComponentParameter(3, nameof(LastRunCell.DialogOpenChanged), EventCallback.Factory.Create<bool>(this, Opened.Add));
			builder.CloseComponent();
		}
	}
}
