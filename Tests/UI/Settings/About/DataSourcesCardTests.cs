using BlazorBlueprint.Components;
using Bunit;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.About;

namespace UI.Tests.Settings.About;

public class DataSourcesCardTests : BunitContext
{
	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Rows_ListEachSourceWithTmdbFirstAndItsNotEndorsedNotice()
	{
		var cut = Render<DataSourcesCard>();

		await Assert
			.That(cut.FindAll(".source-open").Select(link => link.GetAttribute("aria-label") ?? string.Empty))
			.IsEquivalentTo(
				[
					"Open The Movie Database",
					"Open Plex",
					"Open Movie Database List",
					"Open The TVDB",
					"Open IMDb",
					"Open Metacritic",
					"Open Letterboxd",
					"Open TVmaze",
					"Open Wikidata",
					"Open Anime lists",
				],
				CollectionOrdering.Matching
			);
		await Assert
			.That(cut.FindAll(".source-note")[0].TextContent)
			.IsEqualTo("This product uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.");
	}

	[Test]
	public async Task Rows_CreditTvmazeUnderCcBySa()
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(cut.FindAll(".source-note").Select(note => note.TextContent)).Contains("TVmaze's data is licensed under CC BY-SA 4.0.");
	}

	[Test]
	public async Task Rows_CreditWikidatasIdLookupsAsCc0()
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(cut.FindAll(".source-note").Select(note => note.TextContent)).Contains("Wikidata's data is available under CC0.");
	}

	[Test]
	public async Task Logos_AreNeverPartOfALinkSoImdbsRuleHolds()
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(cut.FindAll(".source-logo img")).IsNotEmpty();
		await Assert.That(cut.FindAll("a img")).IsEmpty();
	}

	[Test]
	public async Task Credit_ForTheCcByIcons_NamesTheOneWeModified()
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(cut.Find(".source-credit").TextContent).Contains("(CC BY 4.0; the TVDB tile is modified from theirs)");
	}

	[Test]
	public async Task Credit_NamesEveryRatingSource()
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(cut.FindAll(".source-gives").Select(gives => gives.TextContent)).Contains(gives => gives.Contains("Rotten Tomatoes ratings"));
		await Assert.That(cut.Find(".source-credit").TextContent).Contains("Rotten Tomatoes and its marks are trademarks of Fandango.");
	}

	[Test]
	public async Task Credit_NamesTheServicesRottenTomatoesScoresArriveThrough()
	{
		var cut = Render<DataSourcesCard>();

		await Assert.That(cut.Find(".source-credit").TextContent).Contains("Rotten Tomatoes scores arrive through Plex and MDBList;");
	}

	[Test]
	public async Task Links_EveryOneOpensInANewTab()
	{
		var cut = Render<DataSourcesCard>();

		var links = cut.FindAll("a");
		await Assert.That(links.Select(link => link.GetAttribute("target") ?? string.Empty)).IsEquivalentTo(Enumerable.Repeat("_blank", links.Count));
		await Assert.That(links.Select(link => link.GetAttribute("rel") ?? string.Empty)).IsEquivalentTo(Enumerable.Repeat("noopener noreferrer", links.Count));
	}
}
