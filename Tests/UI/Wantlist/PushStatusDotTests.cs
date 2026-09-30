using BlazorBlueprint.Components;
using Bunit;
using UI.Features.Wantlist.Components;

namespace UI.Tests.Wantlist;

public class PushStatusDotTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	[Arguments(PushDotState.Pending, 0, "Synchronization pending")]
	[Arguments(PushDotState.Pending, 2, "Synchronization pending (attempt 3)")]
	[Arguments(PushDotState.Failed, 1, "Synchronization failed after 1 attempt")]
	[Arguments(PushDotState.Failed, 3, "Synchronization failed after 3 attempts")]
	[Arguments(PushDotState.Pushed, 0, "Synced to your media service")]
	public async Task Render_ForAPushStatus_EmitsTheExpectedAccessibilityLabel(PushDotState state, int attempts, string expected)
	{
		var cut = Render<PushStatusDot>(parameters => parameters.Add(dot => dot.State, state).Add(dot => dot.Attempts, attempts));

		await Assert.That(cut.Find("[aria-label]").GetAttribute("aria-label")).IsEqualTo(expected);
	}
}
