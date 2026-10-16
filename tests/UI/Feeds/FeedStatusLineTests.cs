using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Bunit;
using Contracts;
using Contracts.Media;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Feeds.Components;
using UI.Features.Feeds.Pages;
using static UI.Tests.Feeds.TestFeedRows;

namespace UI.Tests.Feeds;

public class FeedStatusLineTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(IMediaService.Mock().Object);
		var clock = IDateTimeProvider.Mock();
		clock.Now.Getter.Returns(Now);
		clock.Zone.Getter.Returns(TimeZoneInfo.Utc);
		Services.AddSingleton(clock.Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WithBusyFeeds_CountsThemAndPointsNextAtTheFirstIdleSchedule()
	{
		var cut = Render<FeedStatusLine>(parameters => parameters.Add(line => line.Rows, All).Add(line => line.Now, Now).Add(line => line.Added, 1616));

		var text = cut.Find(".status-line").TextContent;
		await Assert.That(text).Contains("1 running");
		await Assert.That(text).Contains("1 queued");
		await Assert.That(cut.Find(".status-next").GetAttribute("aria-label")).IsEqualTo("Next run: hdlists/comedy-movies-2001-2020");
		await Assert.That(text).Contains($"+{1616:N0}");
	}

	[Test]
	public async Task Render_WhenNothingMovedThisWeek_SaysSoInsteadOfZeroes()
	{
		var cut = Render<FeedStatusLine>(parameters => parameters.Add(line => line.Rows, [WeeklyPicks]).Add(line => line.Now, Now));

		await Assert.That(cut.Find(".status-line").TextContent).Contains("nothing changed");
		await Assert.That(cut.FindAll(".status-next")).IsEmpty();
	}

	[Test]
	public async Task Open_TheNextRun_ShowsTheCardBadgesAndLastRunPill()
	{
		var cut = Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedStatusLine>(1);
			builder.AddComponentParameter(2, nameof(FeedStatusLine.Rows), (IReadOnlyList<FeedRow>)[Comedy]);
			builder.AddComponentParameter(3, nameof(FeedStatusLine.Now), Now);
			builder.CloseComponent();
		});

		await cut.Find(".status-next").ClickAsync(new());

		var details = cut.Find(".next-details");
		await Assert.That(details.QuerySelector(".next-chips")?.TextContent).Contains("Mirror").And.Contains("in 43 min");
		await Assert.That(details.QuerySelector(".next-last .last-run")?.TextContent).Contains("Never");
		await Assert.That(details.TextContent).DoesNotContain("Strategy");
	}
}
