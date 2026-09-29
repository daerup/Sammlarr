using BlazorBlueprint.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using UI.Features.Home.Components;
using UI.Shared.Services.Branding;

namespace UI.Tests.Home;

public class UpNextPanelTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Note_WithMoreFeedsScheduledThanItLists_CountsThemAll()
	{
		IReadOnlyList<UpNextRow> scheduled =
		[
			.. Enumerable.Range(1, 7).Select(day => new UpNextRow($"feed {day}", "At 04:00", $"in {day} days", UpNextKind.Scheduled, null)),
		];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Scheduled, scheduled));

		await Assert.That(cut.Find(".band-note").TextContent).IsEqualTo("7 feeds on a schedule");
		await Assert.That(cut.FindAll(".row")).Count().IsEqualTo(5);
	}

	[Test]
	public async Task Row_ForAFeed_LinksItsNameToTheFeedEditor()
	{
		IReadOnlyList<UpNextRow> queued =
		[
			new("hdlists/documentaries", "running now", "running", UpNextKind.Running, "/feeds/edit/docs"),
			new("Sync to Plex", "queued", "2nd in line", UpNextKind.Queued, null),
		];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Queued, queued));

		var names = cut.FindAll(".row-name");
		await Assert.That(names[0].TagName).IsEqualTo("A");
		await Assert.That(names[0].GetAttribute("href")).IsEqualTo("/feeds/edit/docs");
		await Assert.That(names[1].TagName).IsEqualTo("SPAN");
	}

	[Test]
	public async Task Note_WithNothingQueuedOrScheduled_SaysNothingIsScheduled()
	{
		var cut = Render<UpNextPanel>();

		await Assert.That(cut.Find(".band-note").TextContent).IsEqualTo("nothing scheduled");
	}
}
