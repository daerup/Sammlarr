using BlazorBlueprint.Components;
using Bunit;
using Domain.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor;
using UI.Shared.Services.Branding;

namespace UI.Tests.Feeds;

public class FeedRatingStrategyPickerTests : BunitContext
{
	private static readonly ValueSet<RatingSource> Everything =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
	];

	private readonly List<Optional<RatingStrategy>> _changes = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton<MediaProviderBrandingService>();
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Picker_WhenTheFeedFollowsYourDefault_ListsTheDefaultsSourcesReadOnly()
	{
		var cut = RenderPicker(new None());

		await Assert.That(cut.Find(".rating-picker-follows").TextContent).Contains("Follows your default");
		await Assert.That(cut.FindAll(".is-readonly .rating-picker-check").Count).IsEqualTo(4);
	}

	[Test]
	public async Task AverageTab_WhenChosen_GivesTheFeedTheDefaultsAverage()
	{
		var cut = RenderPicker(new None());

		await cut.FindAll("[role=tab]").Single(tab => tab.TextContent.Trim() == "Average").ClickAsync();

		await Assert.That(_changes.Last()).IsSome().And.IsEqualTo(new RatingStrategy(AveragedRating.Everything));
	}

	[Test]
	public async Task Switch_WhenASourceIsSwitchedOff_AveragesTheRest()
	{
		var cut = RenderPicker(Some.Of(new RatingStrategy(AveragedRating.Everything)));

		await cut.Find("button[role=switch][aria-label='TMDB counts']").ClickAsync();

		await Assert
			.That(_changes.Last())
			.IsSome()
			.And.IsEqualTo(new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience])));
	}

	[Test]
	public async Task Switch_ForTheLastSourceSwitchedOn_IsLocked()
	{
		var cut = RenderPicker(Some.Of(new RatingStrategy(new AveragedRating([RatingSource.Imdb]))));

		await Assert.That(cut.Find("button[role=switch][aria-label='IMDb counts']").HasAttribute("disabled")).IsTrue();
	}

	[Test]
	public async Task Grip_WhenAltAndArrowDownArePressed_MovesTheSourceDownOne()
	{
		var cut = RenderPicker(Some.Of(new RatingStrategy(new OrderedRating([RatingSource.Imdb, RatingSource.Tmdb]))));

		await cut.FindAll(".rating-picker-grip")[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown", AltKey = true });

		var ordered = await Assert.That(_changes.Last()).IsSome();
		await Assert
			.That(ordered!)
			.IsOfUnionType<RatingStrategy, OrderedRating>()
			.And.Multiple(async order =>
				await Assert
					.That(order.Order)
					.IsEquivalentTo(
						[RatingSource.Tmdb, RatingSource.Imdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience],
						CollectionOrdering.Matching
					)
			);
	}

	[Test]
	public async Task DefaultTab_WhenChosenOverTheFeedsOwnStrategy_FollowsYourDefaultAgain()
	{
		var cut = RenderPicker(Some.Of(new RatingStrategy(new OrderedRating([RatingSource.Imdb]))));

		await cut.FindAll("[role=tab]").Single(tab => tab.TextContent.Trim() == "My default").ClickAsync();

		await Assert.That(_changes.Last()).IsNone();
	}

	[Test]
	public async Task Grip_WhenTheFirstSourceIsMovedUp_ChangesNothing()
	{
		var cut = RenderPicker(Some.Of(new RatingStrategy(new OrderedRating([RatingSource.Imdb]))));

		await cut.FindAll(".rating-picker-grip")[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true });

		await Assert.That(_changes).IsEmpty();
	}

	[Test]
	public async Task Row_WhenDroppedOnTheFirstRow_TakesItsPlace()
	{
		var cut = RenderPicker(Some.Of(new RatingStrategy(new OrderedRating())));

		await cut.FindAll("[role=listitem]")[2].DragStartAsync(new DragEventArgs());
		await cut.FindAll("[role=listitem]")[0].DropAsync(new DragEventArgs());

		var ordered = await Assert.That(_changes.Last()).IsSome();
		await Assert
			.That(ordered!)
			.IsOfUnionType<RatingStrategy, OrderedRating>()
			.And.Multiple(async order =>
				await Assert
					.That(order.Order)
					.IsEquivalentTo(
						[RatingSource.RottenTomatoesCritics, RatingSource.Tmdb, RatingSource.Imdb, RatingSource.RottenTomatoesAudience],
						CollectionOrdering.Matching
					)
			);
	}

	private IRenderedComponent<FeedRatingStrategyPicker> RenderPicker(Optional<RatingStrategy> value)
	{
		return Render<FeedRatingStrategyPicker>(parameters =>
			parameters
				.Add(picker => picker.Value, value)
				.Add(picker => picker.Default, new RatingStrategy(AveragedRating.Everything))
				.Add(picker => picker.Available, Everything)
				.Add(picker => picker.ValueChanged, EventCallback.Factory.Create<Optional<RatingStrategy>>(this, _changes.Add))
		);
	}
}
