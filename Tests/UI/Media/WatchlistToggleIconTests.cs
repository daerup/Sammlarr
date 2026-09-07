using BlazorBlueprint.Components;
using Bunit;
using Domain.Media;
using UI.Shared.Media.Icons;

namespace UI.Tests.Media;

public class WatchlistToggleIconTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Clicking_the_icon_invokes_the_toggle_callback_with_its_media()
	{
		var media = Lookup();
		MediaLookup? toggled = null;
		var cut = Render<WatchlistToggleIcon>(parameters => parameters.Add(icon => icon.Result, media).Add(icon => icon.OnToggle, value => toggled = value));

		await cut.Find("button").ClickAsync();

		await Assert.That(toggled).IsEqualTo(media);
	}

	[Test]
	public async Task A_toggled_icon_is_marked()
	{
		var cut = Render<WatchlistToggleIcon>(parameters => parameters.Add(icon => icon.Result, Lookup()).Add(icon => icon.IsToggled, true));

		await Assert.That(cut.Find("button").ClassList).Contains("is-marked");
	}

	[Test]
	public async Task An_untoggled_icon_is_not_marked()
	{
		var cut = Render<WatchlistToggleIcon>(parameters => parameters.Add(icon => icon.Result, Lookup()).Add(icon => icon.IsToggled, false));

		await Assert.That(cut.Find("button").ClassList).DoesNotContain("is-marked");
	}

	private static MediaLookup Lookup() => new PartialMovie { Identity = new MediaIdentity(MediaId.From(Guid.NewGuid()), []) }.ToMatch();
}
