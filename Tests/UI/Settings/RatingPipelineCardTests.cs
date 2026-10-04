using AngleSharp.Dom;
using BlazorBlueprint.Components;
using Bunit;
using Contracts.Ratings;
using Domain.Media;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Ratings;
using UI.Shared.Services;
using UI.Shared.Services.Branding;

namespace UI.Tests.Settings;

public class RatingPipelineCardTests : BunitContext
{
	private static readonly ValueSet<RatingSource> Everything =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
	];

	private readonly Mock<IRatingService> _ratings = IRatingService.Mock();

	private RatingContext _context = null!;

	[Before(Test)]
	public void Setup()
	{
		_ratings.GetDefaultAsync(Any()).Returns(new RatingStrategy(AveragedRating.Everything));
		_ratings.GetAvailableAsync(Any()).Returns(Everything);
		_context = new RatingContext(_ratings.Object);
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		Services.AddSingleton(_ratings.Object);
		Services.AddSingleton(_context);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Switch_WhenASourceIsSwitchedOff_StoresTheDefaultAndRereadsIt()
	{
		await _context.GetAsync();
		var cut = RenderCard(new RatingStrategy(AveragedRating.Everything), Everything);

		await cut.Find("button[role=switch][aria-label='TMDB counts']").ClickAsync();
		await _context.GetAsync();

		var rest = new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience]));
		_ratings.StoreDefaultAsync(rest, Any()).WasCalled(Times.Once);
		_ratings.GetDefaultAsync(Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task Combination_WhenFirstAvailableIsChosen_StoresAnOrder()
	{
		var cut = RenderCard(new RatingStrategy(AveragedRating.Everything), Everything);

		await Radio(cut, "First available").ClickAsync();

		_ratings.StoreDefaultAsync(new RatingStrategy(new OrderedRating()), Any()).WasCalled(Times.Once);
		await Assert.That(cut.FindAll(".pipeline-grip").Count).IsEqualTo(4);
	}

	[Test]
	public async Task Switch_ForTheLastSourceSwitchedOn_IsLocked()
	{
		var cut = RenderCard(new RatingStrategy(new AveragedRating([RatingSource.Imdb])), Everything);

		await Assert.That(cut.Find("button[role=switch][aria-label='IMDb counts']").HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task Notice_WhenNoneOfTheChoiceIsAvailable_SaysWhatIsUsedInstead()
	{
		var cut = RenderCard(new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])), [RatingSource.Tmdb]);

		await Assert.That(cut.Find(".pipeline-notice").TextContent.Trim()).IsEqualTo("Your choice (IMDb, RT critics) needs a provider; TMDB is used instead.");
	}

	[Test]
	public async Task Notice_WhenPartOfTheChoiceIsUnavailable_NamesTheMissingSource()
	{
		var cut = RenderCard(new RatingStrategy(new AveragedRating([RatingSource.Tmdb, RatingSource.Imdb])), [RatingSource.Tmdb]);

		await Assert.That(cut.Find(".pipeline-notice").TextContent.Trim()).IsEqualTo("IMDb needs a provider.");
	}

	[Test]
	public async Task Notice_WhenSeveralChosenSourcesAreMissing_SpeaksOfThemTogether()
	{
		var cut = RenderCard(
			new RatingStrategy(new AveragedRating([RatingSource.Tmdb, RatingSource.Imdb, RatingSource.RottenTomatoesCritics])),
			[RatingSource.Tmdb]
		);

		await Assert.That(cut.Find(".pipeline-notice").TextContent.Trim()).IsEqualTo("IMDb, RT critics need a provider.");
	}

	[Test]
	public async Task Notice_WhenSeveralSourcesStandIn_SaysTheyAreUsedInstead()
	{
		var cut = RenderCard(
			new RatingStrategy(new AveragedRating([RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience])),
			[RatingSource.Tmdb, RatingSource.Imdb]
		);

		await Assert
			.That(cut.Find(".pipeline-notice").TextContent.Trim())
			.IsEqualTo("Your choice (RT critics, RT audience) needs a provider; TMDB, IMDb are used instead.");
	}

	[Test]
	public async Task Combination_WhenAverageIsChosenAgain_KeepsTheSourcesSwitchedOffBefore()
	{
		var cut = RenderCard(new RatingStrategy(AveragedRating.Everything), Everything);
		await cut.Find("button[role=switch][aria-label='TMDB counts']").ClickAsync();

		await Radio(cut, "First available").ClickAsync();
		await Radio(cut, "Average").ClickAsync();

		var rest = new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience]));
		_ratings.StoreDefaultAsync(rest, Any()).WasCalled(Times.Exactly(2));
	}

	[Test]
	public async Task Grip_WhenAltAndArrowUpArePressed_StoresTheMovedOrderAndKeepsFocusOnIt()
	{
		var cut = RenderCard(new RatingStrategy(new OrderedRating()), Everything);

		await cut.Find(".pipeline-grip[aria-label^='Move IMDb;']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true });

		_ratings.StoreDefaultAsync(new RatingStrategy(new OrderedRating([RatingSource.Imdb])), Any()).WasCalled(Times.Once);
		JSInterop.VerifyFocusAsyncInvoke();
	}

	[Test]
	public async Task Tile_WhenDroppedOnTheFirstTile_TakesItsPlace()
	{
		var cut = RenderCard(new RatingStrategy(new OrderedRating()), Everything);

		await cut.FindAll(".pipeline-tile")[3].DragStartAsync(new DragEventArgs());
		await cut.FindAll(".pipeline-tile")[0].DropAsync(new DragEventArgs());

		_ratings.StoreDefaultAsync(new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesAudience])), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task Notice_WhenEveryChosenSourceIsAvailable_IsNotShown()
	{
		var cut = RenderCard(new RatingStrategy(new AveragedRating([RatingSource.Imdb])), Everything);

		await Assert.That(cut.FindAll(".pipeline-notice")).IsEmpty();
	}

	[Test]
	public async Task Combination_WhenSwitchedFromFirstAvailableToAverage_KeepsTheRowOrder()
	{
		var cut = RenderCard(new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesAudience, RatingSource.Imdb])), Everything);
		var ranked = Names(cut);

		await Radio(cut, "Average").ClickAsync();

		await Assert.That(Names(cut)).IsEquivalentTo(ranked, CollectionOrdering.Matching);
		await Assert.That(ranked[0]).IsEqualTo("Rotten Tomatoes audience");
	}

	private static List<string> Names(IRenderedComponent<RatingPipelineCard> cut) => [.. cut.FindAll(".pipeline-name").Select(name => name.TextContent.Trim())];

	private static IElement Radio(IRenderedComponent<RatingPipelineCard> cut, string name)
	{
		return cut.FindAll("[role=radio]").Single(radio => radio.TextContent.Trim() == name);
	}

	private IRenderedComponent<RatingPipelineCard> RenderCard(RatingStrategy strategy, ValueSet<RatingSource> available)
	{
		return Render<RatingPipelineCard>(parameters => parameters.Add(card => card.Strategy, strategy).Add(card => card.Available, available));
	}
}
