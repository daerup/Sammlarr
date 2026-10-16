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
		MatchedMedia? toggled = null;
		var cut = Render<WantlistToggleIcon>(parameters => parameters.Add(icon => icon.Result, media).Add(icon => icon.OnToggle, value => toggled = value));

		await cut.Find("button").ClickAsync();

		await Assert.That(toggled).IsEqualTo(media);
	}

	[Test]
	[Arguments(true, "Remove from wantlist")]
	[Arguments(false, "Add to wantlist")]
	public async Task Render_ForEitherState_LabelsTheButtonWithWhatAClickDoes(bool toggled, string label)
	{
		var cut = Render<WantlistToggleIcon>(parameters => parameters.Add(icon => icon.Result, Movie()).Add(icon => icon.IsToggled, toggled));

		await Assert.That(cut.Find("button").GetAttribute("aria-label")).IsEqualTo(label);
	}

	private static MatchedMedia Movie() => new(new MediaIdentity(MediaId.New(), []), new MovieDetails());
}
