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
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using UI.Features.Feeds.Pages.Editor;
using UI.Features.Feeds.Pages.Editor.Filters;
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

		await Radio(cut, "Mirror").ClickAsync();

		await Assert.That(RunLine(cut).TextContent).Contains("Removes an item after").And.Contains("missed runs.");
		await Assert.That(cut.Find("input[aria-label='Runs missing before removal']").GetAttribute("value")).IsEqualTo("3");
		var mirror = await Assert.That(_changes.Last().Strategy).IsOfUnionType<FeedStrategy, Mirror>();
		await Assert.That(mirror.AbsenceThreshold).IsEqualTo(3);
	}

	[Test]
	public async Task Strategy_WhenTheSavedThresholdIsOne_ReadsSingular()
	{
		var cut = RenderPanel(FeedProvider.Mdblist, new Mirror(1));

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

		await Radio(cut, "Mirror").ClickAsync();

		await Assert.That(_changes.Last().RatingStrategy).IsSome().And.IsEqualTo(ordered);
	}

	[Test]
	public async Task Rule_WhenItFiltersOnRating_CarriesTheStarThatNamesTheFeedsRating()
	{
		var cut = RenderPanelFiltering("Rating", 7.0);

		await Assert
			.That(cut.Find(".field-adorned button.field-segment.is-gold").GetAttribute("aria-label"))
			.IsEqualTo("This feed's Sammlarr rating: My default (Average). Click to change.");
		await Assert.That(cut.FindAll(".number-rule-box .field-segment")).IsEmpty();
	}

	[Test]
	public async Task Rule_WhenItFiltersOnVotes_CarriesTheInfoSegmentThatExplainsTheCount()
	{
		var cut = RenderPanelFiltering("Votes", 500.0);

		await Assert.That(cut.FindAll(".field-adorned button.field-segment[aria-label='How Votes are counted']")).IsNotEmpty();
		await Assert.That(cut.FindAll(".number-rule-box .field-segment")).IsEmpty();
		await Assert.That(cut.FindAll("button.field-segment.is-gold")).IsEmpty();
	}

	[Test]
	public async Task Rule_ForARatingBetween_JoinsTheStarToTheField()
	{
		var cut = RenderPanelFiltering("Rating", 7.0, BlazorBlueprint.Primitives.Filtering.FilterOperator.Between, 9.0);

		await Assert.That(cut.FindAll("[aria-label='Filter condition'] button.field-segment.is-gold")).HasSingleItem();
		await Assert.That(cut.FindAll("[aria-label='Filter condition'] .field-adorned button.field-segment.is-gold")).HasSingleItem();
	}

	[Test]
	public async Task Rule_ForAFieldWithoutAStarOrInfo_KeepsAPlainFieldSelect()
	{
		var cut = RenderPanelFiltering("Seasons", 2.0);

		await Assert.That(cut.FindAll("[aria-label='Filter condition'] .field-adorned")).IsEmpty();
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
	[Arguments(nameof(MediaQueryRow.Rating), true, false)]
	[Arguments(nameof(MediaQueryRow.Votes), false, true)]
	[Arguments(nameof(MediaQueryRow.Seasons), false, false)]
	public async Task Sort_ForEachField_JoinsTheStarForRatingTheInfoForVotesAndNoSegmentOtherwise(string field, bool hasStar, bool hasInfo)
	{
		var cut = RenderPanelSorted(field);

		await Assert.That(cut.FindAll(".field-adorned button.field-segment.is-gold").Count > 0).IsEqualTo(hasStar);
		await Assert.That(cut.FindAll(".field-adorned button.field-segment[aria-label='How Votes are counted']").Count > 0).IsEqualTo(hasInfo);
		await Assert.That(cut.FindAll(".field-adorned .field-segment").Count).IsEqualTo(Convert.ToInt32(hasStar) + Convert.ToInt32(hasInfo));
	}

	[Test]
	public async Task Sort_WhenRuntimeTurnsToTotal_SortsByTheTotalAndStillShowsRuntime()
	{
		var cut = RenderPanelSorted(nameof(MediaQueryRow.RuntimeMinutes));

		await cut.InvokeAsync(() => cut.FindComponent<FeedRuntimeModeButton>().Instance.TotalChanged.InvokeAsync(true));

		await Assert.That(_changes.Last().Sort!.Property).IsEqualTo(nameof(MediaQueryRow.TotalRuntimeMinutes));
		await Assert.That(cut.Find(".field-adorned [data-runtime-mode]").GetAttribute("aria-label")).IsEqualTo("Total runtime");
		await Assert.That(cut.Find(".field-adorned [role=combobox]").TextContent.Trim()).IsEqualTo("Runtime");
	}

	[Test]
	public async Task Sort_WhenRuntimeIsPickedAgainOnATotal_KeepsTheTotal()
	{
		var cut = RenderPanelSorted(nameof(MediaQueryRow.TotalRuntimeMinutes));

		await cut.InvokeAsync(() =>
			cut.FindComponents<BbSelect<string>>()
				.Single(select => select.Instance.Value == nameof(MediaQueryRow.RuntimeMinutes))
				.Instance.ValueChanged.InvokeAsync(nameof(MediaQueryRow.RuntimeMinutes))
		);

		await Assert.That(_changes).IsEmpty();
		await Assert.That(cut.Find(".field-adorned [data-runtime-mode]").GetAttribute("aria-label")).IsEqualTo("Total runtime");
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

		Checked(cut, new IdentifierCheck("88434", Error.From("MDBList didn't answer.", new RemoteUnavailableException(RemoteService.Mdblist, sent: true))));

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
	public async Task Identifier_WhenTheFieldReportsTheLookedUpNumberLate_KeepsTheListItResolvedTo()
	{
		var cut = RenderConfig(Documentaries with { Identifier = "88434" });
		Checked(cut, new IdentifierCheck("88434", Success.Of("snoak/trakt-s-trending-shows")));

		await cut.InvokeAsync(() => IdentifierInput(cut).Instance.ValueChanged.InvokeAsync("88434"));

		await Assert.That(IdentifierField(cut).GetAttribute("value")).IsEqualTo("snoak/trakt-s-trending-shows");
		await Assert.That(_changes).IsEmpty();
	}

	[Test]
	public async Task Identifier_OnEnter_Commits()
	{
		var commits = 0;
		var cut = RenderConfig(Documentaries, () => commits++);

		await IdentifierField(cut).KeyUpAsync(new KeyboardEventArgs { Key = "Enter" });

		await Assert.That(commits).IsEqualTo(1);
	}

	[Test]
	public async Task Identifier_WhenTheFieldReportsItsValue_TakesThatValueThenCommits()
	{
		var commits = 0;
		var cut = RenderConfig(Documentaries, () => commits++);

		await IdentifierField(cut).ChangeAsync(new ChangeEventArgs { Value = "hdlists/comedy" });

		await Assert.That(commits).IsEqualTo(1);
		await Assert.That(_changes.Last().Identifier).IsEqualTo("hdlists/comedy");
	}

	[Test]
	public async Task Identifier_OnAnyOtherKey_CommitsNothing()
	{
		var commits = 0;
		var cut = RenderConfig(Documentaries, () => commits++);

		await IdentifierField(cut).KeyUpAsync(new KeyboardEventArgs { Key = "a" });

		await Assert.That(commits).IsEqualTo(0);
	}

	[Test]
	public async Task Identifier_WhenPasted_CommitsOnceThePastedValueArrives()
	{
		var commits = 0;
		var cut = RenderConfig(Documentaries, () => commits++);

		await IdentifierField(cut).TriggerEventAsync("onpaste", new ClipboardEventArgs());
		await Assert.That(commits).IsEqualTo(0);
		await cut.InvokeAsync(() => IdentifierInput(cut).Instance.ValueChanged.InvokeAsync("hdlists/comedy"));

		await Assert.That(commits).IsEqualTo(1);
		await Assert.That(_changes.Last().Identifier).IsEqualTo("hdlists/comedy");
	}

	[Test]
	public async Task Identifier_WhileTyped_CommitsNothing()
	{
		var commits = 0;
		var cut = RenderConfig(Documentaries, () => commits++);

		await cut.InvokeAsync(() => IdentifierInput(cut).Instance.ValueChanged.InvokeAsync("hdlists/com"));

		await Assert.That(commits).IsEqualTo(0);
		await Assert.That(_changes.Last().Identifier).IsEqualTo("hdlists/com");
	}

	[Test]
	public async Task Identifier_WhilePending_ShowsTheEnterKeyInsideTheField()
	{
		var cut = Render<FeedConfigPanel>(parameters => parameters.Add(panel => panel.Value, Documentaries).Add(panel => panel.Pending, true));

		await Assert.That(cut.FindAll("[data-identifier-pending]")).HasSingleItem();
		await Assert.That(IdentifierField(cut).HasAttribute("disabled")).IsFalse();
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

	[Test]
	public async Task Limit_PickingAtRandom_EmitsPickRandomAndShowsTheHelper()
	{
		var cut = RenderConfig(Documentaries with { Limit = new QueryLimit { Count = 5 } });
		await Assert.That(cut.Markup).DoesNotContain("Picks 5 at random");

		await Radio(cut, "At random").ClickAsync();

		await Assert.That(_changes.Last().Limit?.Pick).IsEqualTo(LimitPick.Random);
		await Assert.That(cut.Markup).Contains("Picks 5 at random");
	}

	[Test]
	public async Task Filter_WithAMediaTypeConditionAndNothingPicked_SaysToFinishItAndCannotBeSaved()
	{
		var condition = new FilterCondition
		{
			Field = "MediaType",
			Operator = BlazorBlueprint.Primitives.Filtering.FilterOperator.In,
			Value = Array.Empty<string>(),
		};
		var config = Documentaries with { Filter = new FilterDefinition { Conditions = [condition] } };

		var cut = RenderConfig(config);

		await Assert.That(cut.Find("[data-filter-unfinished]").TextContent.Trim()).IsEqualTo("Finish or remove the Media Type condition to save.");
		await Assert.That(config.Problem).IsSome();
	}

	private static IElement IdentifierField(IRenderedComponent<FeedConfigPanel> cut) => cut.Find("#list-identifier");

	private static IRenderedComponent<BbInput> IdentifierInput(IRenderedComponent<FeedConfigPanel> cut) =>
		cut.FindComponents<BbInput>().Single(input => input.Instance.Id == "list-identifier");

	private static void Checked(IRenderedComponent<FeedConfigPanel> cut, IdentifierCheck check)
	{
		cut.Render(parameters => parameters.Add(panel => panel.Check, check));
	}

	private static IElement ProviderLabel(IRenderedComponent<FeedConfigPanel> cut)
	{
		return cut.FindAll("label").First(label => label.TextContent.Trim().StartsWith("Provider"));
	}

	private static IElement Radio(IRenderedComponent<FeedConfigPanel> cut, string name)
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

	private IRenderedComponent<FeedConfigPanel> RenderConfig(FeedConfig config, Action? onCommit = null)
	{
		return Render<FeedConfigPanel>(parameters =>
			parameters
				.Add(panel => panel.Value, config)
				.Add(panel => panel.OnChange, EventCallback.Factory.Create<FeedConfig>(this, _changes.Add))
				.Add(panel => panel.OnCommit, EventCallback.Factory.Create(this, onCommit ?? (() => { })))
		);
	}
}
