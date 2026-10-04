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
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(IMediaService.Mock().Object);
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddSingleton(ArtworkStub.Sizing());
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Render_WhenTheTitleIsUnmatched_StillShowsItsTaglineAndShowFacts()
	{
		MediaMatchResult unmatched = new UnmatchedMedia(
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

	[Test]
	public async Task Poster_WhenItsSizedCopyFailsToLoad_FallsBackToTheOriginalForGood()
	{
		MediaMatchResult found = new UnmatchedMedia([TmdbId.From("95396", MediaType.Show)], new ShowDetails { Title = "Severance", PosterUrl = "poster.jpg" });
		var cut = Render<MediaDetailTile>(parameters => parameters.Add(tile => tile.Media, found));
		await Assert.That(cut.Find("img").GetAttribute("srcset")).IsEqualTo("poster.jpg?width=192 1x, poster.jpg?width=384 2x");

		await cut.Find("img").TriggerEventAsync("onerror", EventArgs.Empty);
		cut.Render(parameters => parameters.Add(tile => tile.Media, found));

		await Assert.That(cut.Find("img").HasAttribute("srcset")).IsFalse();
	}
}
