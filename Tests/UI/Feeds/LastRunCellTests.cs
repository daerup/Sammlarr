using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts.Blocklist;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Blocklist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using Domain.Wantlist;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Components;
using UI.Shared.Services;
using UI.Tests.Media;

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
	public async Task Dialog_ForATitleNoSinkCouldAdd_ListsItUnderItsOwnGroup()
	{
		RegisterServices();
		var stuck = new FeedRunItem { MediaId = MediaId.New(), State = FeedRunItemState.Unaddressable };

		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.LastRun, Run() with { Items = [.. Run().Items, stuck] }));
		cut.WaitForState(() => cut.FindAll(".last-run").Count == 1);
		await cut.Find(".last-run").ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".change-group").Count == 2);
		await Assert.That(cut.FindAll(".change-group")[1].TextContent).Contains("Couldn't be added");
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
			SourceFailure = Some.Of("No list found with identifier 'hdlists/comedy'."),
		};

		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.LastRun, failed));
		cut.WaitForState(() => cut.FindAll(".last-run").Count == 1);
		await cut.Find(".last-run").ClickAsync(new());
		cut.WaitForState(() => cut.FindAll(".change-band").Count == 1);

		await Assert.That(cut.Find(".change-badges").TextContent).Contains("Failed");
		await Assert.That(cut.Find(".change-band").TextContent).Contains("No list found with identifier 'hdlists/comedy'.");
	}

	[Test]
	public async Task Dialog_ForARunWhoseTitlesThePassRemoved_SaysTheyAreNoLongerListed()
	{
		RegisterServices();

		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.LastRun, Run() with { Items = [] }));
		cut.WaitForState(() => cut.FindAll(".last-run").Count == 1);
		await cut.Find(".last-run").ClickAsync(new());
		cut.WaitForState(() => cut.FindAll(".change-empty").Count == 1);

		await Assert.That(cut.Find(".change-empty").TextContent).IsEqualTo(FeedRunView.TitlesGone);
	}

	[Test]
	public async Task Render_WithBar_ShowsTheBarAndBothTooltipSections()
	{
		RegisterServices();

		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Bar, true));
		cut.WaitForState(() => cut.FindAll("[data-testid=last-run-bar]").Count == 1);
		await cut.Find("[data-testid=last-run-bar]").ParentElement!.TriggerEventAsync("onmouseenter", new MouseEventArgs());
		cut.WaitForState(() => cut.FindAll("[data-testid=last-run-tip]").Count == 1);

		await Assert.That(cut.Find("[data-testid=last-run-tip]").TextContent).Contains("Finding the titles").And.Contains("What the run did");
		await Assert.That(cut.Find("[data-testid=last-run-tip-did]").TextContent).Contains("Added to your collection");
	}

	[Test]
	public async Task Dialog_WhenTheBarIsClicked_Opens()
	{
		RegisterServices();

		var cut = Render<Harness>(parameters => parameters.Add(harness => harness.Bar, true));
		cut.WaitForState(() => cut.FindAll("[data-testid=last-run-bar]").Count == 1);
		await cut.Find("[data-testid=last-run-bar]").ClickAsync(new());

		cut.WaitForState(() => cut.Instance.Opened.Count == 1);
		await Assert.That(cut.Instance.Opened).IsEquivalentTo([true]);
	}

	[Test]
	public async Task Render_WithoutBar_ShowsOnlyThePill()
	{
		RegisterServices();

		var cut = Render<Harness>();
		cut.WaitForState(() => cut.FindAll(".last-run").Count == 1);

		await Assert.That(cut.FindAll("[data-testid=last-run-bar]")).IsEmpty();
	}

	[Test]
	public async Task Render_RunningWithoutALastRun_ShowsTheRunningPill()
	{
		RegisterServices();
		var live = ILiveRun.Mock();
		live.Tally.Getter.Returns(RunTally.Before(new None()));
		var overview = new FeedOverview(TestFeeds.Feed(Feed.Value), new None(), FeedRunHistory.None, 0);

		var cut = Render<LastRunCell>(parameters =>
			parameters.Add(cell => cell.Overview, overview).Add(cell => cell.Running, Some.Of(live.Object)).Add(cell => cell.Bar, true)
		);

		await Assert.That(cut.Find("[data-testid=running-pill]").TextContent).Contains("titles");
		await Assert.That(cut.FindAll("[data-testid=running-bar]")).Count().IsEqualTo(1);
		await Assert.That(cut.Markup).DoesNotContain("Never");
	}

	private void RegisterServices()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(RatingStub.Context());
		Services.AddLogging();

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any(), Any()).Returns((ids, _, _) => Async([.. ids.Select(id => (Result<MatchedMedia>)Success.Of(Detail(id)))]));
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
		Services.AddProviderBranding();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddMediaTitleLink();
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
				UnmatchedNotFound = 0,
				UnmatchedCouldNotLookUp = 0,
				UnmatchedConflict = 0,
				Removed = 0,
				Known = 0,
				NotFoundEarlier = 0,
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

	private static MatchedMedia Detail(MediaId id)
	{
		return new MatchedMedia(
			new MediaIdentity(id, [TmdbId.From(id.Value.ToString(), MediaType.Movie)]),
			new MovieDetails
			{
				Title = "Futurama",
				ReleaseDate = new DateOnly(1999, 3, 28),
				Genres = [],
				Runtime = TimeSpan.FromMinutes(22),
				Ratings = MediaRatings.None,
			}
		);
	}

	private sealed class Harness : ComponentBase
	{
		internal List<bool> Opened { get; } = [];

		[Parameter]
		public FeedRun LastRun { get; set; } = Run();

		[Parameter]
		public bool Bar { get; set; }

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
						FirstRunAt = Some.Of(LastRun.StartedAt),
						TotalAdded = 1,
						TotalRemoved = 0,
					},
					0
				)
			);
			builder.AddComponentParameter(3, nameof(LastRunCell.DialogOpenChanged), EventCallback.Factory.Create<bool>(this, Opened.Add));
			builder.AddComponentParameter(4, nameof(LastRunCell.Bar), Bar);
			builder.CloseComponent();
		}
	}
}
