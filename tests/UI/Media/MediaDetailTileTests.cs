using BlazorBlueprint.Components;
using Bunit;
using Contracts.Media;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using UI.Shared.Media;

namespace UI.Tests.Media;

public class MediaDetailTileTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(IMediaService.Mock().Object);
		Services.AddProviderBranding();
		Services.AddMediaTitleLink();
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
	public async Task Row_WithMarksAndANote_PutsTheMarksAfterTheTitleAndTheNoteUnderTheFacts()
	{
		MediaMatchResult found = new UnmatchedMedia([TmdbId.From("95396", MediaType.Show)], new ShowDetails { Title = "Severance" });

		var cut = Render<MediaDetailTile>(parameters =>
			parameters
				.Add(tile => tile.Media, found)
				.Add(tile => tile.TitleMarks, builder => builder.AddMarkupContent(0, "<i data-mark></i>"))
				.Add(tile => tile.Note, builder => builder.AddMarkupContent(0, "Past the import limit"))
		);

		await Assert.That(cut.FindAll(".search-result-title .title-marks [data-mark]")).HasSingleItem();
		await Assert.That(cut.Find(".search-result-content > .search-result-note").TextContent).IsEqualTo("Past the import limit");
	}

	[Test]
	public async Task Row_WithoutMarksOrANote_RendersNeither()
	{
		MediaMatchResult found = new UnmatchedMedia([TmdbId.From("95396", MediaType.Show)], new ShowDetails { Title = "Severance" });

		var cut = Render<MediaDetailTile>(parameters => parameters.Add(tile => tile.Media, found));

		await Assert.That(cut.FindAll(".title-marks, .search-result-note")).IsEmpty();
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

	[Test]
	public async Task Open_FromThePhoneButton_AsksForTheDetailsAndKeepsTheIcons()
	{
		var opened = 0;
		RenderFragment icons = builder => builder.AddMarkupContent(0, "<span class=\"state\">!</span>");

		var cut = Render<MediaDetailTile>(parameters =>
			parameters.Add(tile => tile.Media, Severance).Add(tile => tile.Icons, icons).Add(tile => tile.OnOpen, () => opened++)
		);
		await cut.Find(".result-open").ClickAsync(new());

		await Assert.That(opened).IsEqualTo(1);
		await Assert.That(cut.Find(".result-open").GetAttribute("aria-label")).IsEqualTo("Details for Severance");
		await Assert.That(cut.Find(".result-icons .state").TextContent).IsEqualTo("!");
	}

	[Test]
	public async Task Render_WithoutAnOpenHandler_HasNoPhoneButton()
	{
		var cut = Render<MediaDetailTile>(parameters => parameters.Add(tile => tile.Media, Severance));

		await Assert.That(cut.FindAll(".result-open")).IsEmpty();
		await Assert.That(cut.FindAll(".result-chevron")).IsEmpty();
	}

	private static MediaMatchResult Severance => new UnmatchedMedia([TmdbId.From("95396", MediaType.Show)], new ShowDetails { Title = "Severance" });
}
