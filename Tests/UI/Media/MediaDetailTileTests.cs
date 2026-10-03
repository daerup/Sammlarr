using BlazorBlueprint.Components;
using Bunit;
using Contracts.Media;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Media;
using UI.Shared.Services.Branding;

namespace UI.Tests.Media;

public class MediaDetailTileTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(IMediaService.Mock().Object);
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WhenTheTitleIsUnmatched_StillShowsItsTaglineAndShowFacts()
	{
		MediaLookup unmatched = new UnmatchedMedia(
			[TmdbId.From("95396", MediaType.Show)],
			new ShowDetails
			{
				Title = "Severance",
				Tagline = "Work is life.",
				Seasons = 2,
				Episodes = 19,
			}
		);

		var cut = Render<MediaDetailTile>(parameters => parameters.Add(tile => tile.Media, unmatched));

		await Assert.That(cut.Markup).Contains("Work is life.");
		await Assert.That(cut.Markup).Contains("2 seasons");
		await Assert.That(cut.Markup).Contains("19 episodes");
	}
}
