using BlazorBlueprint.Components;
using Bunit;
using Domain.Media;
using UI.Shared.Media.Icons;

namespace UI.Tests.Media;

public class WantlistToggleIconTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Click_WhenTheIconIsPressed_InvokesTheToggleCallbackWithItsMedia()
	{
		var media = Movie();
		IdentifiedMedia? toggled = null;
		var cut = Render<WantlistToggleIcon>(parameters => parameters.Add(icon => icon.Result, media).Add(icon => icon.OnToggle, value => toggled = value));

		await cut.Find("button").ClickAsync();

		await Assert.That(toggled).IsEqualTo(media);
	}

	[Test]
	public async Task Render_WhenToggled_MarksTheIcon()
	{
		var cut = Render<WantlistToggleIcon>(parameters => parameters.Add(icon => icon.Result, Movie()).Add(icon => icon.IsToggled, true));

		await Assert.That(cut.Find("button").ClassList).Contains("is-marked");
	}

	[Test]
	public async Task Render_WhenNotToggled_LeavesTheIconUnmarked()
	{
		var cut = Render<WantlistToggleIcon>(parameters => parameters.Add(icon => icon.Result, Movie()).Add(icon => icon.IsToggled, false));

		await Assert.That(cut.Find("button").ClassList).DoesNotContain("is-marked");
	}

	private static IdentifiedMedia Movie() => new(new MediaIdentity(MediaId.From(Guid.NewGuid()), []), new MovieDetails());
}
