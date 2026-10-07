using System.ComponentModel;
using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Filtering;
using Bunit;
using Contracts.Processing;
using Domain.Feed;
using Domain.Media;
using Domain.Processing;
using Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Editor;
using UI.Tests.Media;
using UI.Tests.Providers;

namespace UI.Tests.Feeds;

public class FeedConfigPanelTests : BunitContext
{
	private static readonly FeedConfig Documentaries = new()
	{
		Name = "Documentaries",
		Provider = FeedProvider.Mdblist,
		Identifier = "hdlists/documentaries",
	};

	private readonly List<FeedConfig> _changes = [];

	private readonly HashSet<RemoteService> _connected = [RemoteService.Mdblist];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddProviderBranding();
		Services.AddSingleton(RatingStub.Context());
		Services.AddSingleton(ReadinessStub.Connected(_connected));
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Provider_WhenAnOpenedFeedsProviderHasNoKey_StaysChosenSaysWhereToAddItAndBlocksTesting()
	{
		_connected.Remove(RemoteService.Mdblist);

		var cut = RenderPanel(FeedProvider.Mdblist);

		var config = _changes.Last();
		await Assert.That(config.Provider).IsEqualTo(FeedProvider.Mdblist);
		await Assert.That(config.IsTestable).IsFalse();
		await Assert.That(ProviderLabel(cut).TextContent).Contains("*");
		await Assert.That(cut.Markup).Contains("API key is missing, fix in");
		await Assert
			.That(cut.FindAll("a").Single(link => link.TextContent.Trim() == "Settings").GetAttribute("href"))
			.IsEqualTo("/settings?section=providers&provider=mdblist");
	}

	[Test]
	public async Task Provider_OnANewFeed_StartsEmptyAndAsksForOne()
	{
		var cut = RenderPanel(null);

		await Assert.That(cut.Find("[role=combobox]").TextContent).Contains("Select a provider");
		await Assert.That(_changes).IsEmpty();
		await Assert.That(cut.Markup).DoesNotContain("API key is missing");
	}

	[Test]
	public async Task Strategy_OnAppend_ShowsNoRunCount()
	{
		var cut = RenderPanel(FeedProvider.Mdblist);

		await Assert.That(cut.Markup).DoesNotContain("Removes an item after");
	}

	[Test]
	public async Task Strategy_WhenMirrorIsPickedOnANewFeed_AsksForThreeMissedRuns()
	{
		var cut = RenderPanel(FeedProvider.Mdblist);

		await StrategyRadio(cut, "Mirror").ClickAsync();

		await Assert.That(RunLine(cut).TextContent).Contains("Removes an item after").And.Contains("missed runs.");
		await Assert.That(cut.Find("input[aria-label='Runs missing before removal']").GetAttribute("value")).IsEqualTo("3");
		var mirror = await Assert.That(_changes.Last().Strategy).IsOfUnionType<FeedStrategy, Mirror>();
		await Assert.That(mirror.AbsenceThreshold).IsEqualTo(3);
	}

	[Test]
	public async Task Strategy_WhenTheSavedThresholdIsOne_ReadsSingularInAmber()
	{
		var cut = RenderPanel(FeedProvider.Mdblist, new Mirror(1));

		await Assert.That(RunLine(cut).ClassList.Contains("text-amber-500")).IsTrue();
		await Assert.That(RunLine(cut).TextContent).Contains("missed run.");
	}

	[Test]
	public async Task Strategy_WhenTheRunCountSteps_RaisesMirrorWithTheNewCount()
	{
		var cut = RenderPanel(FeedProvider.Mdblist, new Mirror(3));

		await cut.Find(".run-count-line button[aria-label='Increase value']").ClickAsync();

		var mirror = await Assert.That(_changes.Last().Strategy).IsOfUnionType<FeedStrategy, Mirror>();
		await Assert.That(mirror.AbsenceThreshold).IsEqualTo(4);
	}

	[Test]
	public async Task Strategy_WhenTheRunCountIsClearedOrZero_KeepsMirrorAtOne()
	{
		var cut = RenderPanel(FeedProvider.Mdblist, new Mirror(3));
		var runCount = cut.FindComponent<BbNumericInput<int>>();

		await cut.InvokeAsync(() => runCount.Instance.JsOnBlur("0"));

		var mirror = await Assert.That(_changes.Last().Strategy).IsOfUnionType<FeedStrategy, Mirror>();
		await Assert.That(mirror.AbsenceThreshold).IsEqualTo(1);
	}

	[Test]
	public async Task RatingStrategy_WhenAnotherFieldChanges_KeepsTheFeedsOwn()
	{
		var ordered = new RatingStrategy(new OrderedRating([RatingSource.Imdb]));
		var cut = RenderPanel(FeedProvider.Mdblist, new Append(), ordered);

		await StrategyRadio(cut, "Mirror").ClickAsync();

		await Assert.That(_changes.Last().RatingStrategy).IsSome().And.IsEqualTo(ordered);
	}

	[Test]
	public async Task Rule_WhenItFiltersOnRating_CarriesTheStarThatNamesTheFeedsRating()
	{
		var cut = RenderPanelFiltering("Rating", 7.0);

		await Assert
			.That(cut.Find(".number-rule-box button.field-segment.is-gold").GetAttribute("aria-label"))
			.IsEqualTo("This feed's Sammlarr rating: My default (Average). Click to change.");
	}

	[Test]
	public async Task Rule_WhenItFiltersOnVotes_CarriesTheInfoSegmentThatExplainsTheCount()
	{
		var cut = RenderPanelFiltering("Votes", 500.0);

		await Assert.That(cut.FindAll(".number-rule-box button.field-segment[aria-label='How Votes are counted']")).IsNotEmpty();
		await Assert.That(cut.FindAll("button.field-segment.is-gold")).IsEmpty();
	}

	[Test]
	[Arguments("Rating", 7.0)]
	[Arguments("Votes", 500.0)]
	public async Task Rule_ForANumber_HasNoStepper(string field, double value)
	{
		var cut = RenderPanelFiltering(field, value);

		await Assert.That(cut.FindAll(".number-rule-box button[aria-label='Increase value']")).IsEmpty();
		await Assert.That(cut.FindAll(".number-rule-box button[aria-label='Decrease value']")).IsEmpty();
	}

	[Test]
	public async Task Sort_WhenByRating_JoinsTheStarToTheSortField()
	{
		var cut = RenderPanelSorted(nameof(MediaQueryRow.Rating));

		await Assert.That(cut.FindAll(".sort-field button.field-segment.is-gold")).IsNotEmpty();
		await Assert.That(cut.FindAll(".sort-field [aria-label='How Votes are counted']")).IsEmpty();
	}

	[Test]
	public async Task Sort_WhenByVotes_JoinsTheInfoToTheSortField()
	{
		var cut = RenderPanelSorted(nameof(MediaQueryRow.Votes));

		await Assert.That(cut.FindAll(".sort-field button.field-segment[aria-label='How Votes are counted']")).IsNotEmpty();
		await Assert.That(cut.FindAll(".sort-field .is-gold")).IsEmpty();
	}

	[Test]
	public async Task Sort_WhenByTitle_HasNoSegment()
	{
		var cut = RenderPanelSorted(nameof(MediaQueryRow.Title));

		await Assert.That(cut.FindAll(".sort-field .field-segment")).IsEmpty();
	}

	[Test]
	public async Task Rule_WhenARatingIsBetweenTwoBounds_ShowsBothBounds()
	{
		var cut = RenderPanelFiltering("Rating", 7.0, BlazorBlueprint.Primitives.Filtering.FilterOperator.Between, 8.5);

		await Assert.That(cut.Find("input[aria-label='Rating']").GetAttribute("value")).IsEqualTo("7");
		await Assert.That(cut.Find("input[aria-label='Rating, upper bound']").GetAttribute("value")).IsEqualTo("8.5");
	}

	[Test]
	public async Task Identifier_AfterANumberWasLookedUp_ShowsWhatIsSavedAndWhereItCameFrom()
	{
		var cut = RenderConfig(Documentaries with { Identifier = "88434" });

		Checked(cut, new IdentifierCheck("88434", Success.Of("snoak/trakt-s-trending-shows")));

		await Assert.That(IdentifierField(cut).GetAttribute("value")).IsEqualTo("snoak/trakt-s-trending-shows");
		await Assert.That(cut.Find("[data-identifier-note]").TextContent.Trim()).IsEqualTo("Looked up from 88434.");
	}

	[Test]
	[Arguments("https://mdblist.com/lists/snoak/trakt-s-trending-shows")]
	[Arguments("www.mdblist.com/lists/snoak/trakt-s-trending-shows")]
	public async Task Identifier_AfterALinkWasRead_SaysItCameFromTheLink(string link)
	{
		var cut = RenderConfig(Documentaries with { Identifier = link });

		Checked(cut, new IdentifierCheck(link, Success.Of("snoak/trakt-s-trending-shows")));

		await Assert.That(cut.Find("[data-identifier-note]").TextContent.Trim()).IsEqualTo("Taken from the mdblist.com link.");
	}

	[Test]
	public async Task Identifier_ThatWasAlreadyTheStoredForm_HasNoNote()
	{
		var cut = RenderConfig(Documentaries);

		Checked(cut, new IdentifierCheck("hdlists/documentaries", Success.Of("hdlists/documentaries")));

		await Assert.That(cut.FindAll("[data-identifier-note]")).IsEmpty();
	}

	[Test]
	public async Task Identifier_WhenRefused_SaysWhyUnderTheFieldAndMarksIt()
	{
		var cut = RenderConfig(Documentaries with { Identifier = "top-250-movies" });

		Checked(cut, new IdentifierCheck("top-250-movies", Error.From("Paste the list's link from mdblist.com, its owner/list, or its number.")));

		await Assert
			.That(cut.Find("[data-identifier-error]").TextContent.Trim())
			.IsEqualTo("Paste the list's link from mdblist.com, its owner/list, or its number.");
		await Assert.That(IdentifierField(cut).GetAttribute("aria-invalid")).IsEqualTo("true");
	}

	[Test]
	public async Task Identifier_WhenTheProviderDidNotAnswer_WarnsWithoutMarkingTheField()
	{
		var cut = RenderConfig(Documentaries with { Identifier = "88434" });

		Checked(cut, new IdentifierCheck("88434", Error.From("MDBList didn't answer.", new RemoteUnavailableException(RemoteService.Mdblist))));

		await Assert.That(cut.Find("[data-identifier-warning]").TextContent.Trim()).IsEqualTo("MDBList didn't answer.");
		await Assert.That(cut.FindAll("[data-identifier-error]")).IsEmpty();
		await Assert.That(IdentifierField(cut).GetAttribute("aria-invalid")).IsNotEqualTo("true");
	}

	[Test]
	public async Task Identifier_OnceEditedAgain_DropsTheNote()
	{
		var cut = RenderConfig(Documentaries with { Identifier = "88434" });
		Checked(cut, new IdentifierCheck("88434", Success.Of("snoak/trakt-s-trending-shows")));

		await cut.InvokeAsync(() =>
			cut.FindComponents<BbInput>().Single(input => input.Instance.Id == "list-identifier").Instance.ValueChanged.InvokeAsync("12345")
		);

		await Assert.That(cut.FindAll("[data-identifier-note]")).IsEmpty();
	}

	[Test]
	[Arguments(FeedProvider.Mdblist, "Browse lists on MDBList", "https://mdblist.com/toplists/")]
	[Arguments(FeedProvider.Tmdb, "Make a list on TMDB", "https://www.themoviedb.org/list/new")]
	[Arguments(FeedProvider.AniList, "Find a user on AniList", "https://anilist.co/search/users")]
	[Arguments(FeedProvider.Mal, "Find a user on MyAnimeList", "https://myanimelist.net/users.php")]
	public async Task Identifier_ForEachProvider_LinksWhereItsListsAreFoundInANewTab(FeedProvider provider, string label, string url)
	{
		var cut = RenderPanel(provider);

		var link = cut.FindAll("a").Single(anchor => anchor.TextContent == label);
		await Assert.That(link.GetAttribute("href")).IsEqualTo(url);
		await Assert.That(link.GetAttribute("target")).IsEqualTo("_blank");
	}

	private static IElement IdentifierField(IRenderedComponent<FeedConfigPanel> cut) => cut.Find("#list-identifier");

	private static void Checked(IRenderedComponent<FeedConfigPanel> cut, IdentifierCheck check)
	{
		cut.Render(parameters => parameters.Add(panel => panel.Check, check));
	}

	private static IElement ProviderLabel(IRenderedComponent<FeedConfigPanel> cut)
	{
		return cut.FindAll("label").First(label => label.TextContent.Trim().StartsWith("Provider"));
	}

	private static IElement StrategyRadio(IRenderedComponent<FeedConfigPanel> cut, string name)
	{
		return cut.FindAll("[role=radio]").First(radio => radio.TextContent.Trim() == name);
	}

	private static IElement RunLine(IRenderedComponent<FeedConfigPanel> cut) => cut.Find(".run-count-line");

	private IRenderedComponent<FeedConfigPanel> RenderPanel(FeedProvider? provider) => RenderPanel(provider, new Append());

	private IRenderedComponent<FeedConfigPanel> RenderPanelFiltering(
		string field,
		double value,
		BlazorBlueprint.Primitives.Filtering.FilterOperator comparison = BlazorBlueprint.Primitives.Filtering.FilterOperator.GreaterOrEqual,
		double? end = null
	)
	{
		var condition = new FilterCondition
		{
			Field = field,
			Operator = comparison,
			Value = value,
			ValueEnd = end,
		};
		return RenderConfig(Documentaries with { Filter = new FilterDefinition { Conditions = [condition] } });
	}

	private IRenderedComponent<FeedConfigPanel> RenderPanelSorted(string field)
	{
		return RenderConfig(
			Documentaries with
			{
				Sort = new QuerySort { Property = field, Direction = ListSortDirection.Descending },
			}
		);
	}

	private IRenderedComponent<FeedConfigPanel> RenderPanel(FeedProvider? provider, FeedStrategy strategy, RatingStrategy? rating = null)
	{
		return RenderConfig(Documentaries with { Provider = provider, Strategy = strategy, RatingStrategy = Optional.Of(rating) });
	}

	private IRenderedComponent<FeedConfigPanel> RenderConfig(FeedConfig config)
	{
		return Render<FeedConfigPanel>(parameters =>
			parameters.Add(panel => panel.Value, config).Add(panel => panel.OnChange, EventCallback.Factory.Create<FeedConfig>(this, _changes.Add))
		);
	}
}
