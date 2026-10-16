using BlazorBlueprint.Components;
using Bunit;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaListEmptyStateTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Actions_WhenGiven_AreShown()
	{
		var cut = Render<MediaListEmptyState>(parameters =>
			parameters
				.Add(state => state.Icon, "rss")
				.Add(state => state.Heading, "No feeds yet.")
				.Add(state => state.Actions, actions => actions.AddMarkupContent(0, "<button>Create your first feed</button>"))
		);

		await Assert.That(cut.FindAll("button").Select(button => button.TextContent)).Contains("Create your first feed");
	}

	[Test]
	public async Task Actions_WhenNotGiven_LeaveNothingBehind()
	{
		var cut = Render<MediaListEmptyState>(parameters => parameters.Add(state => state.Icon, "rss").Add(state => state.Heading, "Nothing blocklisted."));

		await Assert.That(cut.FindAll("button")).IsEmpty();
	}
}
