using BlazorBlueprint.Components;
using Bunit;
using UI.Features.Home.Components;

namespace UI.Tests.Home;

public class UpNextPanelTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Note_WithMoreFeedsScheduledThanItLists_CountsThemAll()
	{
		IReadOnlyList<UpNextRow> scheduled =
		[
			.. Enumerable.Range(1, 7).Select(day => new UpNextRow($"feed {day}", "At 04:00", $"in {day} days", UpNextKind.Scheduled)),
		];

		var cut = Render<UpNextPanel>(parameters => parameters.Add(panel => panel.Scheduled, scheduled));

		await Assert.That(cut.Find(".band-note").TextContent).IsEqualTo("7 feeds on a schedule");
		await Assert.That(cut.FindAll(".row")).Count().IsEqualTo(5);
	}

	[Test]
	public async Task Note_WithNothingQueuedOrScheduled_SaysNothingIsScheduled()
	{
		var cut = Render<UpNextPanel>();

		await Assert.That(cut.Find(".band-note").TextContent).IsEqualTo("nothing scheduled");
	}
}
