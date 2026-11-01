using System.Globalization;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Media;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Home.Components;
using UI.Shared.Common;
using UI.Tests.Media;

namespace UI.Tests.Home;

public class ActivityDayBlockTests : BunitContext
{
	private static readonly FeedId Docs = FeedId.From(new Guid("00000000-0000-0000-0000-0000000000d0"));

	private static readonly Dictionary<FeedId, string> Names = new() { [Docs] = "hdlists/latest-hd-documentary-movies" };

	private static readonly DateTimeOffset Noon = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

	private static readonly DateOnly Today = DateOnly.FromDateTime(Noon.DateTime);

	private const string Tile = "[data-testid=media-tile]";

	private const string Faded = "[data-testid=media-tile] .media-card-dimmed";

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(ArtworkStub.Sizing());
		Services.AddProviderBranding();
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(IMediaService.Mock().Object);
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
	public async Task Name_InAGermanBrowser_StaysEnglishLikeTheRestOfTheUi()
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");

		var cut = Render(Day(added: 1), DateOnly.FromDateTime(Noon.DateTime).AddDays(2));

		await Assert.That(cut.Find(".dname").TextContent).IsEqualTo("Sunday 27 September");
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

	[Test]
	public async Task Phone_OpenDay_ShowsItsTitlesAsAPosterGridInsteadOfTracks()
	{
		var day = Day(added: 2, removed: 1);
		var requested = new List<MediaId>();

		var cut = this.RenderOnPhone<ActivityDayBlock>(parameters =>
			parameters
				.Add(block => block.Day, day)
				.Add(block => block.Today, Today)
				.Add(block => block.Media, Titled(day))
				.Add(block => block.OnNeedMedia, ids => requested.AddRange(ids))
		).Component;

		await Assert.That(cut.FindAll(".track-wrap")).IsEmpty();
		await Assert.That(cut.FindAll(Tile)).Count().IsEqualTo(3);
		await Assert.That(cut.FindAll(Faded)).HasSingleItem();
		await Assert.That(requested).IsEquivalentTo([.. day.Added, .. day.Removed]);
	}

	[Test]
	public async Task Phone_DayWithManyTitles_ShowsSevenFullRowsAndTheRestOnceScrolledToTheEnd()
	{
		var day = Day(added: 25);
		var cut = this.RenderOnPhone<ActivityDayBlock>(parameters =>
			parameters.Add(block => block.Day, day).Add(block => block.Today, Today).Add(block => block.Media, Titled(day))
		).Component;
		await Assert.That(cut.FindAll(Tile)).Count().IsEqualTo(21);
		await Assert.That(cut.FindAll(".dtoggle").Where(button => button.TextContent.Contains("more", StringComparison.Ordinal))).IsEmpty();

		await ReachTheEndAsync(cut);

		await Assert.That(cut.FindAll(Tile)).Count().IsEqualTo(25);
	}

	[Test]
	public async Task Phone_OlderDayWithAProblem_StartsFoldedAndCountsTheProblem()
	{
		var failed = Run(0, 0) with { Conclusiveness = Conclusiveness.StillLoading, SourceFailure = Some.Of("No MDBList API key is configured.") };
		var day = ActivityLog.Days([failed], Names, TimeZoneInfo.Utc)[0];

		var cut = this.RenderOnPhone<ActivityDayBlock>(parameters =>
			parameters
				.Add(block => block.Day, day)
				.Add(block => block.Today, Today)
				.Add(block => block.Media, new Dictionary<MediaId, MatchedMedia>())
				.Add(block => block.StartsFolded, true)
		).Component;

		await Assert.That(cut.Find(".dtop").GetAttribute("aria-expanded")).IsEqualTo("false");
		await Assert.That(cut.Find(".dstat .is-problem").TextContent).IsEqualTo("1 problem");
	}

	[Test]
	public async Task Phone_DayWithMoreAddedThanTheCap_FillsTheRestWithRemovedTitlesAndAsksForTheRestAtTheEnd()
	{
		var day = Day(added: 18, removed: 5);
		var requested = new List<MediaId>();
		var cut = this.RenderOnPhone<ActivityDayBlock>(parameters =>
			parameters
				.Add(block => block.Day, day)
				.Add(block => block.Today, Today)
				.Add(block => block.Media, Titled(day))
				.Add(block => block.OnNeedMedia, ids => requested.AddRange(ids))
		).Component;
		await Assert.That(cut.FindAll(Faded)).Count().IsEqualTo(3);

		await ReachTheEndAsync(cut);

		await Assert.That(requested).IsEquivalentTo([.. day.Added, .. day.Removed]);
		await Assert.That(cut.FindAll(Faded)).Count().IsEqualTo(5);
	}

	[Test]
	public async Task Phone_FoldedDay_AsksForItsTitlesOnlyOnceOpened()
	{
		var day = Day(added: 2);
		var requested = new List<MediaId>();
		var cut = this.RenderOnPhone<ActivityDayBlock>(parameters =>
			parameters
				.Add(block => block.Day, day)
				.Add(block => block.Today, Today)
				.Add(block => block.Media, Titled(day))
				.Add(block => block.StartsFolded, true)
				.Add(block => block.OnNeedMedia, ids => requested.AddRange(ids))
		).Component;
		await Assert.That(requested).IsEmpty();

		await cut.Find(".dtop").ClickAsync(new());

		await Assert.That(requested).IsEquivalentTo(day.Added);
		await Assert.That(cut.FindAll(Tile)).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Phone_OpenDayWhenAnotherDayArrives_StaysOpen()
	{
		var day = Day(added: 1);
		var cut = this.RenderOnPhone<ActivityDayBlock>(parameters =>
			parameters.Add(block => block.Day, day).Add(block => block.Today, Today).Add(block => block.Media, Titled(day))
		).Component;

		cut.Render(parameters => parameters.Add(block => block.StartsFolded, true));

		await Assert.That(cut.Find(".dtop").GetAttribute("aria-expanded")).IsEqualTo("true");
	}

	[Test]
	public async Task Phone_DayWithOnlyRemovals_LeavesOutTheAddedGrid()
	{
		var day = Day(removed: 2);

		var cut = this.RenderOnPhone<ActivityDayBlock>(parameters =>
			parameters.Add(block => block.Day, day).Add(block => block.Today, Today).Add(block => block.Media, Titled(day))
		).Component;

		await Assert.That(cut.FindAll(".day-grid")).HasSingleItem();
		await Assert.That(cut.FindAll(Faded)).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Desktop_OlderDay_StaysOpen()
	{
		var cut = Render<ActivityDayBlock>(parameters =>
			parameters
				.Add(block => block.Day, Day(added: 1))
				.Add(block => block.Media, new Dictionary<MediaId, MatchedMedia>())
				.Add(block => block.Today, Today)
				.Add(block => block.StartsFolded, true)
		);

		await Assert.That(cut.Find(".dtop").GetAttribute("aria-expanded")).IsEqualTo("true");
	}

	private static Task ReachTheEndAsync(IRenderedComponent<ActivityDayBlock> cut) =>
		cut.InvokeAsync(() => cut.FindComponent<PaginationControls>().Instance.OnEndInView(true));

	private static Dictionary<MediaId, MatchedMedia> Titled(ActivityDay day)
	{
		return day
			.Added.Concat(day.Removed)
			.ToDictionary(id => id, id => new MatchedMedia(new MediaIdentity(id, []), new MovieDetails { Title = $"Title {id.Value}" }));
	}

	private IRenderedComponent<ActivityDayBlock> Render(ActivityDay day, DateOnly? today = null) =>
		Render<ActivityDayBlock>(parameters =>
			parameters.Add(block => block.Day, day).Add(block => block.Media, new Dictionary<MediaId, MatchedMedia>()).Add(block => block.Today, today ?? Today)
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
