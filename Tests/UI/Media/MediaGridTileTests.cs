using BlazorBlueprint.Components;
using Bunit;
using Contracts.Media;
using Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Media;
using ErrorEventArgs = Microsoft.AspNetCore.Components.Web.ErrorEventArgs;

namespace UI.Tests.Media;

public class MediaGridTileTests : BunitContext
{
	private const string Backdrop = "https://cdn.example.test/backdrop.jpg";
	private const string Poster = "https://cdn.example.test/poster.jpg";

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(IMediaService.Mock().Object);
		Services.AddSingleton(ArtworkStub.Sizing());
	}

	[Test]
	public async Task Render_WhenTheBackdropCanBeResized_OffersItAtTheSizesTheTileDisplays()
	{
		var cut = Render<MediaGridTile>(parameters => parameters.Add(tile => tile.Media, Movie()));

		var image = cut.Find("img.media-card-poster");
		await Assert.That(image.GetAttribute("src")).IsEqualTo(Backdrop);
		await Assert.That(image.GetAttribute("srcset")).IsEqualTo($"{Backdrop}?width=780 780w, {Backdrop}?width=1280 1280w");
	}

	[Test]
	public async Task ImageError_WhenTheSizedBackdropFails_RetriesTheOriginalBeforeFallingBackToThePoster()
	{
		var cut = Render<MediaGridTile>(parameters => parameters.Add(tile => tile.Media, Movie()));

		await cut.Find("img.media-card-poster").TriggerEventAsync("onerror", new ErrorEventArgs());
		var original = cut.Find("img.media-card-poster");
		await Assert.That(original.GetAttribute("src")).IsEqualTo(Backdrop);
		await Assert.That(original.HasAttribute("srcset")).IsFalse();

		await original.TriggerEventAsync("onerror", new ErrorEventArgs());
		var poster = cut.Find("img.media-card-poster");
		await Assert.That(poster.GetAttribute("src")).IsEqualTo(Poster);
		await Assert.That(poster.GetAttribute("srcset")).Contains($"{Poster}?width=780 780w");
	}

	[Test]
	public async Task Render_WhenThereIsNoBackdrop_ShowsThePosterStraightAway()
	{
		var cut = Render<MediaGridTile>(parameters => parameters.Add(tile => tile.Media, Movie(backdrop: null)));

		await Assert.That(cut.Find("img.media-card-poster").GetAttribute("src")).IsEqualTo(Poster);
	}

	[Test]
	public async Task Render_WhenThereIsNoArtworkAtAll_ShowsTheNoImageState()
	{
		var cut = Render<MediaGridTile>(parameters => parameters.Add(tile => tile.Media, Movie(backdrop: null, poster: null)));

		await Assert.That(cut.FindAll(".media-card-failed")).Count().IsEqualTo(1);
		await Assert.That(cut.FindAll("img.media-card-poster")).IsEmpty();
	}

	[Test]
	public async Task Render_WhenArtworkArrivesLater_ShowsTheBackdrop()
	{
		var cut = Render<MediaGridTile>(parameters => parameters.Add(tile => tile.Media, Movie(backdrop: null, poster: null)));

		cut.Render(parameters => parameters.Add(tile => tile.Media, Movie()));

		await Assert.That(cut.Find("img.media-card-poster").GetAttribute("src")).IsEqualTo(Backdrop);
	}

	private static MediaMatchResult Movie(string? backdrop = Backdrop, string? poster = Poster)
	{
		return new MatchedMedia(new MediaIdentity(MediaId.From(Guid.NewGuid()), []), new MovieDetails { BackdropUrl = backdrop, PosterUrl = poster });
	}
}
