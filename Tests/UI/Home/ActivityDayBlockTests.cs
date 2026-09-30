using BlazorBlueprint.Components;
using Bunit;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
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

		await Assert.That(cut.Find(".dstat").TextContent).DoesNotContain("·");
		await Assert.That(cut.FindAll(".dstat .part")).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Stat_WhenNothingWasRemoved_LeavesOutTheRemovedFigure()
	{
		var cut = Render(Day(added: 3));

		await Assert.That(cut.FindAll(".dstat .part")).Count().IsEqualTo(2);
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
		var failed = Run(0, 0) with { Conclusiveness = Conclusiveness.StillLoading, SourceFailure = "No MDBList API key is configured." };
		var cut = Render(ActivityLog.Days([failed], Names, TimeZoneInfo.Utc)[0]);

		await cut.Find(".dtoggle").ClickAsync(new());

		await Assert.That(cut.Find(".dproblem").TextContent).Contains("failed at").And.Contains("No MDBList API key is configured.");
		await Assert.That(cut.Find(".drun-delta").TextContent.Trim()).IsEqualTo("failed");
	}

	private IRenderedComponent<ActivityDayBlock> Render(ActivityDay day) =>
		Render<ActivityDayBlock>(parameters =>
			parameters
				.Add(block => block.Day, day)
				.Add(block => block.Media, new Dictionary<MediaId, MediaLookup>())
				.Add(block => block.Today, DateOnly.FromDateTime(Noon.DateTime))
		);

	private static ActivityDay Day(int added = 0, int removed = 0) => ActivityLog.Days([Run(added, removed)], Names, TimeZoneInfo.Utc)[0];

	private static FeedRun Run(int added, int removed) =>
		new()
		{
			Id = Guid.NewGuid(),
			FeedId = Docs,
			StartedAt = Noon,
			Duration = TimeSpan.FromSeconds(4),
			Strategy = new Mirror(),
			Trigger = new Scheduled("0 0 4 * * *"),
			Conclusiveness = Conclusiveness.Conclusive,
			Counts = FeedRunCounts.None with { Total = added + removed, Added = added, Removed = removed },
			Items =
			[
				.. Enumerable.Range(0, added).Select(_ => Item(FeedRunItemState.Added)),
				.. Enumerable.Range(0, removed).Select(_ => Item(FeedRunItemState.Removed)),
			],
		};

	private static FeedRunItem Item(FeedRunItemState state) => new() { MediaId = MediaId.From(Guid.NewGuid()), State = state };
}
