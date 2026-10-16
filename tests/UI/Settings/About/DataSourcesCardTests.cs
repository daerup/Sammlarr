using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.About;

namespace UI.Tests.Settings.About;

public class DataSourcesCardTests : BunitContext
{
	private const string Band = "[data-testid='source-band']";

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Rows_ListTheServicesSammlarrAsksWithTmdbFirst_ThenThePagesAndRatingsBand()
	{
		var cut = Render<DataSourcesCard>();

		await Assert
			.That(Opens(cut.FindAll(".source-row")))
			.IsEquivalentTo(
				[
					"Open The Movie Database",
					"Open Plex",
					"Open IMDb",
					"Open Movie Database List",
					"Open Trakt",
					"Open AniList",
					"Open MyAnimeList",
					"Open TVmaze",
					"Open Wikidata",
					"Open Anime lists",
					"Open The TVDB",
					"Open Rotten Tomatoes®",
					"Open Metacritic",
					"Open Letterboxd",
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Band_HoldsTheTvdbFirstAndThenTheRatingSites_AndTheTvdbIsNotAServiceAsked()
	{
		var cut = Render<DataSourcesCard>();

		var band = Keys(cut.FindAll($"{Band} .source-row")).ToList();
		var asked = Keys(cut.FindAll(".source-row").Where(row => row.Closest(Band) is null));
		await Assert.That(cut.Find(".source-band-title").TextContent).IsEqualTo("Pages and ratings");
		await Assert.That(band).IsEquivalentTo(["Tvdb", "RottenTomatoes", "Metacritic", "Letterboxd"], CollectionOrdering.Matching);
		await Assert.That(asked).DoesNotContain("Tvdb");
		await Assert.That(Chips(cut, "Tvdb")).IsEquivalentTo(["Pages"]);
		await Assert.That(cut.Find($"{Band} [data-source='RottenTomatoes'] .source-via").TextContent).IsEqualTo("through Plex and MDBList");
	}

	[Test]
	[Arguments("Tmdb", "This product uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.")]
	[Arguments("Plex", "Plex and the Plex Play logo are trademarks of Plex and used under a license.")]
	[Arguments("ImdbDatasets", "Information courtesy of IMDb (https://www.imdb.com). Used with permission.")]
	[Arguments("Tvmaze", "TVmaze's data is licensed under CC BY-SA 4.0.")]
	[Arguments(
		"AnimeLists",
		"Contains information from anime-offline-database, which is made available here under the Open Database License (ODbL). That database is cedya77's continuation of manami-project's work. Fribb's anime-lists is built from it and declares no licence; Sammlarr downloads it at runtime and never ships it."
	)]
	[Arguments("RottenTomatoes", "Rotten Tomatoes® and Tomatometer® are registered trademarks of Fandango Media, LLC.")]
	public async Task Note_OfAnOwnerThatAsksForOne_IsItsWordsExactly(string source, string note)
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(cut.Find($"[data-source='{source}'] .source-note").TextContent).IsEqualTo(note);
	}

	[Test]
	public async Task Notes_StandOnlyWhereAnOwnerAsksForOne()
	{
		var cut = Render<DataSourcesCard>();

		await Assert
			.That(Keys(cut.FindAll(".source-row").Where(row => row.QuerySelector(".source-note") is not null)))
			.IsEquivalentTo(["Tmdb", "Plex", "ImdbDatasets", "Tvmaze", "AnimeLists", "RottenTomatoes"]);
	}

	[Test]
	public async Task Notes_LinkTheLicencesAndTheProjectsTheyName()
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(Links(cut, "[data-source='ImdbDatasets'] .source-note a")).IsEquivalentTo(["https://www.imdb.com"]);
		await Assert.That(Links(cut, "[data-source='Tvmaze'] .source-note a")).IsEquivalentTo(["https://creativecommons.org/licenses/by-sa/4.0/"]);
		await Assert
			.That(Links(cut, "[data-source='AnimeLists'] .source-note a"))
			.IsEquivalentTo(
				[
					"https://github.com/cedya77/anime-offline-database",
					"https://opendatacommons.org/licenses/odbl/1-0/",
					"https://github.com/manami-project/anime-offline-database",
				],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task Footer_SaysTheNamesAndLogosOnlyIdentifyEachService()
	{
		var cut = Render<DataSourcesCard>();

		await Assert
			.That(cut.Find(".source-foot > span").TextContent)
			.IsEqualTo("Names and logos belong to their owners and only identify each service; no endorsement is implied.");
	}

	[Test]
	public async Task Chips_ComeFromTheConverterAndEnricherRegistrations()
	{
		var imdbToTmdb = new ById(Accepts: [MediaProvider.Imdb], Yields: [MediaProvider.Tmdb], Cost: 1);
		Services.AddSingleton(Registrations.Converter(RemoteService.Tmdb, imdbToTmdb));
		Services.AddSingleton(Registrations.Enricher(RemoteService.Tmdb, MediaField.Title | MediaField.Poster));
		Services.AddSingleton(Registrations.Converter(RemoteService.Mdblist, imdbToTmdb));
		Services.AddSingleton(Registrations.Enricher(RemoteService.Mdblist, MediaFields.Rated([RatingSource.Imdb, RatingSource.Metacritic])));

		var cut = Render<DataSourcesCard>();

		await Assert.That(Chips(cut, "Tmdb")).IsEquivalentTo(["Search", "Trending", "Feeds", "Matching", "Details", "Ratings"], CollectionOrdering.Matching);
		await Assert.That(Chips(cut, "Mdblist")).IsEquivalentTo(["Feeds", "Matching", "Ratings"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Chips_WithoutAConverterOrEnricher_LeaveMatchingAndDetailsOut()
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(Chips(cut, "Tmdb")).IsEquivalentTo(["Search", "Trending", "Feeds", "Ratings"], CollectionOrdering.Matching);
		await Assert.That(Chips(cut, "Wikidata")).IsEmpty();
	}

	[Test]
	public async Task Logos_AreNeverPartOfALinkSoImdbsRuleHolds()
	{
		var cut = Render<DataSourcesCard>();
		await OpenCreditsAsync(cut);

		await Assert.That(cut.FindAll(".source-logo img")).IsNotEmpty();
		await Assert.That(cut.FindAll("a img")).IsEmpty();
	}

	[Test]
	public async Task Links_EveryOneOpensInANewTab()
	{
		var cut = Render<DataSourcesCard>();
		await OpenCreditsAsync(cut);

		var links = cut.FindAll("a");
		await Assert.That(links.Select(link => link.GetAttribute("target") ?? string.Empty)).IsEquivalentTo(Enumerable.Repeat("_blank", links.Count));
		await Assert.That(links.Select(link => link.GetAttribute("rel") ?? string.Empty)).IsEquivalentTo(Enumerable.Repeat("noopener noreferrer", links.Count));
	}

	[Test]
	public async Task LogoCredits_StayClosedUntilOpened_ThenListTheFilesGroupedBySource()
	{
		var cut = Render<DataSourcesCard>();
		await Assert.That(cut.FindAll("[data-testid='logo-credits']")).IsEmpty();

		await OpenCreditsAsync(cut);

		await Assert
			.That(cut.FindAll("[data-origin]").Select(group => group.GetAttribute("data-origin") ?? string.Empty))
			.IsEquivalentTo(["Selfhst", "SimpleIcons", "WikimediaCommons", "Owner", "Sammlarr"], CollectionOrdering.Matching);
		await Assert
			.That(cut.FindAll("[data-origin='Selfhst'] .credit-file").Select(file => file.TextContent))
			.IsEquivalentTo([
				"imdb-logo.svg",
				"radarr-square-dark.svg",
				"radarr-square-light.svg",
				"sonarr-square-dark.svg",
				"sonarr-square-light.svg",
				"tvdb-square.svg",
				"tvdb-tile-dark.svg",
				"tvdb-tile-light.svg",
			]);
	}

	[Test]
	public async Task LogoCredits_ForSelfhst_LinkItsLicenceAndSayWhatChanged()
	{
		var cut = Render<DataSourcesCard>();
		await OpenCreditsAsync(cut);

		var radarr = Credit(cut, "radarr-square-dark.svg");
		await Assert.That(Links(cut, "[data-origin='Selfhst'] .credit-origin a")).IsEquivalentTo(["https://creativecommons.org/licenses/by/4.0/"]);
		await Assert.That(radarr.QuerySelector(".credit-changes")?.TextContent).IsEqualTo("outline recoloured from #24292e to #e6e6e6 for the dark theme");
	}

	[Test]
	public async Task LogoCredits_WithASecondSource_NameTheSiteThenTheFile()
	{
		var cut = Render<DataSourcesCard>();
		await OpenCreditsAsync(cut);

		var rottenTomatoes = Credit(cut, "rottentomatoes-logo.png");
		await Assert
			.That(rottenTomatoes.QuerySelectorAll(".credit-source").Select(link => link.TextContent))
			.IsEquivalentTo(["www.rottentomatoes.com", "file"], CollectionOrdering.Matching);
	}

	private static async Task OpenCreditsAsync(IRenderedComponent<DataSourcesCard> cut)
	{
		await cut.FindAll("button").Single(button => button.TextContent.Contains("Logo credits")).ClickAsync();
	}

	private static IElement Credit(IRenderedComponent<DataSourcesCard> cut, string file)
	{
		return cut.FindAll(".credit").Single(credit => credit.QuerySelector(".credit-file")?.TextContent == file);
	}

	private static IEnumerable<string> Opens(IEnumerable<IElement> rows)
	{
		return rows.Select(row => row.QuerySelector(".source-open")?.GetAttribute("aria-label") ?? string.Empty);
	}

	private static IEnumerable<string> Keys(IEnumerable<IElement> rows) => rows.Select(row => row.GetAttribute("data-source") ?? string.Empty);

	private static IEnumerable<string> Chips(IRenderedComponent<DataSourcesCard> cut, string source)
	{
		return cut.FindAll($"[data-source='{source}'] [data-chip]").Select(chip => chip.GetAttribute("data-chip") ?? string.Empty);
	}

	private static IEnumerable<string> Links(IRenderedComponent<DataSourcesCard> cut, string selector)
	{
		return cut.FindAll(selector).Select(link => link.GetAttribute("href") ?? string.Empty);
	}
}
