using BlazorBlueprint.Components;
using Bunit;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Home.Components;
using UI.Tests.Media;

namespace UI.Tests.Home;

public class ActivityDayBlockTests : BunitContext
{
	private static readonly FeedId Docs = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d0"));

	private static readonly Dictionary<FeedId, string> Names = new() { [Docs] = "hdlists/latest-hd-documentary-movies" };

	private static readonly DateTimeOffset Noon = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(ArtworkStub.Sizing());
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Stat_SeparatesTheFiguresWithoutAnyTextGlyph()
	{
		var cut = Render(Day(added: 3, removed: 2));

		var stat = cut.Find(".dstat").TextContent.Words();
		await Assert.That(stat).DoesNotContain("·");
		await Assert.That(stat).Contains("+3 added").And.Contains("2 removed").And.Contains("1 run");
	}

	[Test]
	public async Task Stat_WhenNothingWasRemoved_LeavesOutTheRemovedFigure()
	{
		var cut = Render(Day(added: 3));

		await Assert.That(cut.Find(".dstat").TextContent).DoesNotContain("removed");
	}

	[Test]
	public async Task Header_WhenClicked_CollapsesTheDayToItsHeaderThenOpensItAgain()
	{
		var cut = Render(Day(added: 3, removed: 2));

		await cut.Find(".dtop").ClickAsync(new());

		await Assert.That(cut.Find(".dtop").GetAttribute("aria-expanded")).IsEqualTo("false");
		await Assert.That(cut.FindAll(".chips, .dsub, .dtoggle")).IsEmpty();
		await Assert.That(cut.FindAll(".dstat .part")).Count().IsEqualTo(3);

		await cut.Find(".dtop").ClickAsync(new());

		await Assert.That(cut.Find(".dtop").GetAttribute("aria-expanded")).IsEqualTo("true");
		await Assert.That(cut.FindAll(".chips, .dsub, .dtoggle")).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Problem_ForARunWhoseSourceFailed_SaysItFailedAndQuotesWhy()
	{
		var failed = Run(0, 0) with { Conclusiveness = Conclusiveness.StillLoading, SourceFailure = Some.Of("No MDBList API key is configured.") };
		var cut = Render(ActivityLog.Days([failed], Names, TimeZoneInfo.Utc)[0]);

		await cut.Find(".dtoggle").ClickAsync(new());

		await Assert.That(cut.Find(".dproblem").TextContent).Contains("failed at").And.Contains("No MDBList API key is configured.");
		await Assert.That(cut.Find(".drun-delta").TextContent.Trim()).IsEqualTo("failed");
	}

	[Test]
	public async Task Chip_OfAFeedThatIsGone_SaysSoAndLinksNowhere()
	{
		var cut = Render(ActivityLog.Days([Run(2, 0)], new Dictionary<FeedId, string>(), TimeZoneInfo.Utc)[0]);

		var chip = cut.Find(".chip");
		await Assert.That(chip.TextContent).Contains("A deleted feed");
		await Assert.That(chip.HasAttribute("href")).IsFalse();
	}

	[Test]
	public async Task Chip_OfAFeedThatExists_LinksToItsLatestRun()
	{
		var run = Run(2, 0);

		var cut = Render(ActivityLog.Days([run], Names, TimeZoneInfo.Utc)[0]);

		await Assert.That(cut.Find(".chip").GetAttribute("href")).IsEqualTo($"/feeds/{UrlGuid.Encode(Docs.Value)}/runs/{UrlGuid.Encode(run.Id.Value)}");
	}

	private IRenderedComponent<ActivityDayBlock> Render(ActivityDay day) =>
		Render<ActivityDayBlock>(parameters =>
			parameters
				.Add(block => block.Day, day)
				.Add(block => block.Media, new Dictionary<MediaId, MatchedMedia>())
				.Add(block => block.Today, DateOnly.FromDateTime(Noon.DateTime))
		);

	private static ActivityDay Day(int added = 0, int removed = 0) => ActivityLog.Days([Run(added, removed)], Names, TimeZoneInfo.Utc)[0];

	private static FeedRun Run(int added, int removed) =>
		TestFeeds.Run(TestFeeds.Feed(Docs.Value), Guid.NewGuid()) with
		{
			StartedAt = Noon,
			Counts = FeedRunCounts.None with { Total = added + removed, Added = added, Removed = removed },
			Items =
			[
				.. Enumerable
					.Repeat(FeedRunItemState.Added, added)
					.Concat(Enumerable.Repeat(FeedRunItemState.Removed, removed))
					.Select(state => new FeedRunItem { MediaId = MediaId.New(), State = state }),
			],
		};
}
