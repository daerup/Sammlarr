using System.Globalization;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Blocklist;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Media;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TestInfrastructure;
using UI.Features.Feeds.Components.Runs;
using UI.Features.Feeds.Pages.Runs;
using UI.Shared.Common;
using UI.Shared.Services;

namespace UI.Tests.Feeds;

public class FeedRunsTests : BunitContext
{
	private static readonly FeedId Feed = FeedId.From(new Guid(31, 0, 0, new byte[8]));

	private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

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
		var failed = Run(Now) with { Conclusiveness = Conclusiveness.StillLoading, SourceFailure = Some.Of("No list found with identifier 'hdlists/comedy'.") };
		RegisterServices([failed]);

		var cut = Render<FeedRuns>(parameters => parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)).Add(page => page.RunIdParam, Link(failed)));
		cut.WaitForState(() => cut.FindAll(".runs-band").Count == 1);

		await Assert.That(cut.Find(".runs-band").TextContent).Contains("The source could not be read");
		await Assert.That(cut.Find(".runs-band").TextContent).Contains("No list found with identifier 'hdlists/comedy'.");
	}

	[Test]
	public async Task Render_ARunThatMadeCalls_EndsItsSummaryWithTheCallCount()
	{
		var calling = Run(Now) with { Calls = [new FeedRunCalls(RemoteService.Tmdb, UsageOutcome.Answered, 8287)] };
		RegisterServices([calling]);

		var cut = Render<FeedRuns>(parameters => parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)).Add(page => page.RunIdParam, Link(calling)));
		cut.WaitForState(() => cut.FindAll("[data-testid=run-calls]").Count == 1);

		await Assert.That(cut.Find(".runs-detail-sub").TextContent).Contains("took 2 s · 8,287 calls");
	}

	[Test]
	public async Task Render_ARunThatChangedTitlesButListsNone_SaysItsTitlesAreNoLongerListed()
	{
		var trimmed = Run(Now.AddDays(-120)) with { Counts = FeedRunCounts.None with { Total = 500, Added = 4, Removed = 3, Kept = 493 } };
		RegisterServices([trimmed]);

		var cut = Render<FeedRuns>(parameters => parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)).Add(page => page.RunIdParam, Link(trimmed)));
		cut.WaitForState(() => cut.FindAll("[data-testid=run-titles-gone]").Count == 1);

		await Assert.That(cut.Find("[data-testid=run-titles-gone]").TextContent).Contains(FeedRunView.TitlesGone);
		await Assert.That(cut.Markup).DoesNotContain("matched your collection exactly");
	}

	[Test]
	public async Task Render_InAGermanBrowser_StaysEnglishLikeTheRestOfTheUi()
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");
		var older = Run(Now.AddDays(-3));
		RegisterServices([older]);

		var cut = Render<FeedRuns>(parameters => parameters.Add(page => page.FeedIdParam, UrlGuid.Encode(Feed.Value)).Add(page => page.RunIdParam, Link(older)));
		cut.WaitForState(() => cut.FindAll(".runs-item").Count == 1);

		await Assert.That(cut.Markup).Contains("1 run since 11 October").And.DoesNotContain("Oktober");
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
		Services.AddSingleton<ICurrentUser>(new TestCurrentUser());
		var clock = new FakeTimeProvider(Now);
		Services.AddSingleton<TimeProvider>(clock);

		var feeds = IFeedService.Mock();
		feeds
			.GetAsync(Any())
			.Returns(
				new FeedSnapshot([
					new FeedOverview(
						TestFeeds.Feed(Feed.Value),
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
		media.GetDetailsAsync(Any(), Any(), Any()).Returns(AsyncEnumerable.Empty<Result<MatchedMedia>>());
		Services.AddSingleton(media.Object);

		Services.AddSingleton(IWantlistService.Mock().Object);

		Services.AddSingleton(IBlocklistService.Mock().Object);
		Services.AddSingleton(IProviderConfigService.Mock().Object);
		Services.AddScoped<Notifier>();
		Services.AddProviderBranding();
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

	private static IReadOnlyList<FeedRun> History(int count) => [.. Enumerable.Range(0, count).Select(hoursAgo => Run(Now.AddHours(-hoursAgo)))];

	private static string Link(FeedRun run) => UrlGuid.Encode(run.Id.Value);

	private static FeedRun Run(DateTimeOffset startedAt) =>
		TestFeeds.Run(TestFeeds.Feed(Feed.Value), Guid.NewGuid()) with
		{
			StartedAt = startedAt,
			Duration = TimeSpan.FromSeconds(2),
		};
}
