using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Runs;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Feeds;

public class FeedRunsTests : BunitContext
{
	private static readonly FeedId Feed = FeedId.From(new Guid(31, 0, 0, new byte[8]));

	private static readonly IReadOnlyList<FeedRun> Runs = History(30);

	[Test]
	public async Task Render_WhenTheLinkedRunIsBehindLoadOlder_ListsItAndScrollsToIt()
	{
		RegisterServices(Runs);
		var scroll = JSInterop.SetupVoid("sammlarrScroll.intoView", "run-selected");
		scroll.SetVoidResult();

		var cut = Render<FeedRuns>(parameters => parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)).Add(page => page.RunIdParam, Link(Runs[27])));
		cut.WaitForState(() => scroll.Invocations.Count == 1);

		await Assert.That(cut.FindAll(".runs-item")).Count().IsEqualTo(28);
		await Assert.That(cut.FindAll("#run-selected")).HasSingleItem();
	}

	[Test]
	public async Task LoadOlder_AfterJumpingToALinkedRun_ShowsTheRestAndThenHidesTheButton()
	{
		var runs = History(80);
		RegisterServices(runs);

		var cut = Render<FeedRuns>(parameters => parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)).Add(page => page.RunIdParam, Link(runs[60])));
		cut.WaitForState(() => cut.FindAll(".runs-older").Count == 1);
		await cut.Find(".runs-older").ClickAsync(new());

		cut.WaitForState(() => cut.FindAll(".runs-item").Count == runs.Count);
		await Assert.That(cut.FindAll(".runs-older")).IsEmpty();
	}

	[Test]
	public async Task Render_ForARunWhoseSourceFailed_ShowsWhyAlongsideTheFailure()
	{
		var failed = Run(DateTimeOffset.UtcNow) with
		{
			Conclusiveness = Conclusiveness.StillLoading,
			SourceFailure = Some.Of("No list found with identifier 'hdlists/comedy'."),
		};
		RegisterServices([failed]);

		var cut = Render<FeedRuns>(parameters => parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)).Add(page => page.RunIdParam, Link(failed)));
		cut.WaitForState(() => cut.FindAll(".runs-band").Count == 1);

		await Assert.That(cut.Find(".runs-band").TextContent).Contains("The source could not be read");
		await Assert.That(cut.Find(".runs-band").TextContent).Contains("No list found with identifier 'hdlists/comedy'.");
	}

	[Test]
	public async Task Render_WhileTheFeedIsStillLoading_ShowsASkeleton()
	{
		var feeds = RegisterServices(Runs);
		feeds.GetAsync(Any()).ReturnsAsync(new TaskCompletionSource<FeedSnapshot>().Task);

		var cut = Render<FeedRuns>(parameters => parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)));

		await Assert.That(cut.FindAll(".runs-page[aria-busy='true']")).HasSingleItem();
	}

	private Mock<IFeedService> RegisterServices(IReadOnlyList<FeedRun> runs)
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddLogging();

		var feeds = IFeedService.Mock();
		feeds
			.GetAsync(Any())
			.Returns(
				new FeedSnapshot([
					new FeedOverview(
						new Domain.Feed.Feed
						{
							Name = "Comedy",
							Id = Feed,
							Enabled = true,
							Provider = FeedProvider.Mdblist,
							Strategy = new FeedStrategy(new Append()),
							Identifier = "hdlists/comedy",
							CreatedAt = runs[^1].StartedAt,
							Schedule = new FeedRunTrigger(new Manual()),
						},
						Some.Of(runs[0]),
						new FeedRunHistory
						{
							TotalRuns = runs.Count,
							FirstRunAt = Some.Of(runs[^1].StartedAt),
							TotalAdded = 0,
							TotalRemoved = 0,
						},
						0
					),
				])
			);
		feeds.GetRunsAsync(Any(), Any()).Returns((query, _) => Page(runs, query));
		Services.AddSingleton(feeds.Object);

		var media = IMediaService.Mock();
		media.GetDetailsAsync(Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>());
		Services.AddSingleton(media.Object);

		Services.AddSingleton(IWantlistService.Mock().Object);

		Services.AddSingleton(IBlocklistService.Mock().Object);
		Services.AddSingleton(IProviderConfigService.Mock().Object);
		Services.AddScoped<Notifier>();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddScoped<ScrollService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
		return feeds;
	}

	private static IReadOnlyList<FeedRun> Page(IReadOnlyList<FeedRun> runs, RunQuery query)
	{
		return query.Feed.When(feed => feed == Feed).Map(_ => runs.Take(Math.Max(query.Limit, Reach(runs, query))).ToList()).GetValueOr([]);
	}

	private static int Reach(IReadOnlyList<FeedRun> runs, RunQuery query)
	{
		return query.MustInclude.Match(id => runs.ToList().FindIndex(run => run.Id == id) + 1, () => 0);
	}

	private static IReadOnlyList<FeedRun> History(int count) =>
		[.. Enumerable.Range(0, count).Select(hoursAgo => Run(DateTimeOffset.UtcNow.AddHours(-hoursAgo)))];

	private static string Link(FeedRun run) => UrlGuid.Encode(run.Id.Value);

	private static FeedRun Run(DateTimeOffset startedAt) =>
		new()
		{
			Id = RunId.From(Guid.NewGuid()),
			FeedId = Feed,
			StartedAt = startedAt,
			Duration = TimeSpan.FromSeconds(2),
			Strategy = new FeedStrategy(new Append()),
			Trigger = new FeedRunTrigger(new Manual()),
			Conclusiveness = Conclusiveness.Conclusive,
			Counts = FeedRunCounts.None,
			Items = [],
		};
}
