using AngleSharp.Dom;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using BlazorBlueprint.Primitives.Filtering;
using Bunit;
using Contracts;
using Contracts.Media;
using Contracts.Processing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor;
using Duration = Domain.Processing.Duration;
using DurationUnit = Domain.Processing.DurationUnit;
using GenreMatch = Domain.Processing.GenreMatch;

namespace UI.Tests.Feeds;

public class FeedFilterBuilderTests : BunitContext
{
	private readonly List<FilterDefinition> _changes = [];

	[Before(Test)]
	public void Setup()
	{
		Services.AddBlazorBlueprintComponents();
		Services.AddSingleton(IDateTimeProvider.Mock().Object);
		Services.AddSingleton(IMediaService.Mock().Object);
		JSInterop.Mode = JSRuntimeMode.Loose;
	}

	[Test]
	public async Task Operators_ForGenres_OfferContainsAnyAllAndNoneOnly()
	{
		var cut = RenderBuilder(new FilterDefinition { Conditions = [Condition(nameof(MediaQueryRow.Genres), FilterOperator.In, new GenreMatch(["Drama"], []))] });

		await OperatorTrigger(cut).ClickAsync(new());

		await Assert.That(OptionTexts(cut)).IsEquivalentTo(["contains any", "contains all", "contains none"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Operator_WhenGenresTurnFromAnyToAll_KeepsThePicks()
	{
		var drama = new GenreMatch(["Drama"], []);
		var condition = Condition(nameof(MediaQueryRow.Genres), FilterOperator.In, drama);
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await PickAsync(cut, OperatorTrigger(cut), "contains all");

		await Assert.That(condition.Operator).IsEqualTo(FilterOperator.Contains);
		await Assert.That(condition.Value).IsEqualTo(drama);
	}

	[Test]
	public async Task Operators_ForReleaseDate_NameTheComparisonsInDateWords()
	{
		var cut = RenderBuilder(
			new FilterDefinition { Conditions = [Condition(nameof(MediaQueryRow.ReleaseDate), FilterOperator.GreaterThan, new DateTime(1980, 1, 1))] }
		);

		await OperatorTrigger(cut).ClickAsync(new());

		await Assert.That(OptionTexts(cut)).IsEquivalentTo(["equals", "not equals", "is after", "is before", "between"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Field_WhenChangedToAnotherKind_StartsTheConditionOverAndRaisesTheChange()
	{
		var condition = Condition(nameof(MediaQueryRow.Title), FilterOperator.Contains, "Ink");
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await PickAsync(cut, FieldTrigger(cut), "Rating");

		await Assert.That(condition.Field).IsEqualTo(nameof(MediaQueryRow.Rating));
		await Assert.That(condition.Operator).IsEqualTo(FilterOperator.Equals);
		await Assert.That(condition.Value).IsNull();
		await Assert.That(_changes.Last().Version).IsEqualTo(1);
	}

	[Test]
	public async Task Field_WhenChangedToOneOfTheSameKind_KeepsTheOperatorAndValue()
	{
		var condition = Condition(nameof(MediaQueryRow.Title), FilterOperator.Contains, "Ink");
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await PickAsync(cut, FieldTrigger(cut), "Tagline");

		await Assert.That(condition.Operator).IsEqualTo(FilterOperator.Contains);
		await Assert.That(condition.Value).IsEqualTo("Ink");
	}

	[Test]
	public async Task Field_WhenChangedToGenres_ShowsThePickerWithNothingPicked()
	{
		var condition = Condition(nameof(MediaQueryRow.Title), FilterOperator.Contains, "Ink");
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await PickAsync(cut, FieldTrigger(cut), "Genres");

		await Assert.That(condition.Operator).IsEqualTo(FilterOperator.In);
		await Assert.That(condition.Value).IsNull();
		await Assert.That(cut.FindAll("button[aria-label='Genres']")).HasSingleItem();
	}

	[Test]
	public async Task Runtime_ForTheTotal_JoinsTheTotalModeToTheField()
	{
		var cut = RenderBuilder(
			new FilterDefinition
			{
				Conditions = [Condition(nameof(MediaQueryRow.TotalRuntimeMinutes), FilterOperator.LessOrEqual, new Duration(6, DurationUnit.Hours))],
			}
		);

		await Assert.That(FieldTrigger(cut).TextContent).Contains("Runtime");
		await Assert.That(cut.Find(".field-adorned [data-runtime-mode]").GetAttribute("aria-label")).IsEqualTo("Total runtime");
		await Assert.That(cut.FindAll(".number-rule-box [data-runtime-mode]")).IsEmpty();
		await Assert.That(cut.Find("input[aria-label='Runtime']").GetAttribute("value")).IsEqualTo("6");
	}

	[Test]
	[Arguments(nameof(MediaQueryRow.RuntimeMinutes), "Total", nameof(MediaQueryRow.TotalRuntimeMinutes), "Total runtime")]
	[Arguments(nameof(MediaQueryRow.TotalRuntimeMinutes), "Per episode", nameof(MediaQueryRow.RuntimeMinutes), "Runtime per episode")]
	public async Task Runtime_WhenTheOtherModeIsPickedInThePopover_SwitchesTheField(string field, string mode, string switched, string label)
	{
		var condition = Condition(field, FilterOperator.LessOrEqual, new Duration(30, DurationUnit.Minutes));
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await cut.Find("[data-runtime-mode]").ClickAsync(new());
		await Buttons(cut, mode).Single().ClickAsync(new());

		await Assert.That(condition.Field).IsEqualTo(switched);
		await Assert.That(cut.Find("[data-runtime-mode]").GetAttribute("aria-label")).IsEqualTo(label);
		await Assert.That(_changes.Last().Version).IsEqualTo(1);
	}

	[Test]
	public async Task Runtime_ForABetween_ShowsTheUnitSwitchInBothBoxes()
	{
		var condition = Condition(nameof(MediaQueryRow.RuntimeMinutes), FilterOperator.Between, new Duration(1, DurationUnit.Hours));
		condition.ValueEnd = new Duration(2, DurationUnit.Hours);
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await Assert.That(PickedUnits(cut)).IsEquivalentTo(["h", "h"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Runtime_WhenHoursArePickedBeforeTyping_KeepsTheValueEmptyAndTypesInHours()
	{
		var condition = Condition(nameof(MediaQueryRow.RuntimeMinutes), FilterOperator.LessOrEqual);
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await Buttons(cut, "h").Single().ClickAsync(new());
		var untouched = condition.Value;
		await TypeAsync(cut, 2);

		await Assert.That(untouched).IsNull();
		await Assert.That(condition.Value).IsEqualTo(new Duration(2, DurationUnit.Hours));
	}

	[Test]
	public async Task Runtime_WhenHoursArePickedInTheUpperBox_KeepsTheNumberInTheNewUnitOnBothEnds()
	{
		var condition = Condition(nameof(MediaQueryRow.RuntimeMinutes), FilterOperator.Between, new Duration(1, DurationUnit.Minutes));
		condition.ValueEnd = new Duration(2, DurationUnit.Minutes);
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await Buttons(cut, "h").Last().ClickAsync(new());

		await Assert.That(condition.Value).IsEqualTo(new Duration(1, DurationUnit.Hours));
		await Assert.That(condition.ValueEnd).IsEqualTo(new Duration(2, DurationUnit.Hours));
		await Assert.That(PickedUnits(cut)).IsEquivalentTo(["h", "h"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Runtime_WhenTyped_KeepsTheChosenUnit()
	{
		var condition = Condition(nameof(MediaQueryRow.RuntimeMinutes), FilterOperator.LessOrEqual, new Duration(2, DurationUnit.Hours));
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await TypeAsync(cut, 3);

		await Assert.That(condition.Value).IsEqualTo(new Duration(3, DurationUnit.Hours));
	}

	[Test]
	public async Task Field_WhenRuntimeIsPickedAgainOnATotal_KeepsTheTotal()
	{
		var condition = Condition(nameof(MediaQueryRow.TotalRuntimeMinutes), FilterOperator.LessOrEqual, new Duration(6, DurationUnit.Hours));
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await PickAsync(cut, FieldTrigger(cut), "Runtime");

		await Assert.That(condition.Field).IsEqualTo(nameof(MediaQueryRow.TotalRuntimeMinutes));
		await Assert.That(_changes).IsEmpty();
	}

	[Test]
	public async Task Operator_WhenMediaTypeTurnsToIsAnyOf_DropsTheSingleChoice()
	{
		var condition = Condition(nameof(MediaQueryRow.MediaType), FilterOperator.Equals, "Movie");
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await PickAsync(cut, OperatorTrigger(cut), "is any of");

		await Assert.That(condition.Operator).IsEqualTo(FilterOperator.In);
		await Assert.That(condition.Value).IsNull();
	}

	[Test]
	public async Task Operator_WhenItLeavesBetween_DropsTheUpperBoundAndKeepsTheLower()
	{
		var condition = Condition(nameof(MediaQueryRow.Rating), FilterOperator.Between, 7.0);
		condition.ValueEnd = 8.5;
		var cut = RenderBuilder(new FilterDefinition { Conditions = [condition] });

		await PickAsync(cut, OperatorTrigger(cut), "greater or equal");

		await Assert.That(condition.Value).IsEqualTo(7.0);
		await Assert.That(condition.ValueEnd).IsNull();
	}

	[Test]
	public async Task NestedGroup_WhenOneOfItsConditionsChanges_RaisesTheRootsVersion()
	{
		var nested = Condition(nameof(MediaQueryRow.Title), FilterOperator.Contains, "Ink");
		var cut = RenderBuilder(new FilterDefinition { Groups = [new FilterDefinition { Conditions = [nested] }] });

		await PickAsync(cut, OperatorTrigger(cut), "starts with");

		await Assert.That(_changes.Last().Version).IsEqualTo(1);
		await Assert.That(nested.Operator).IsEqualTo(FilterOperator.StartsWith);
	}

	[Test]
	public async Task AddGroup_TwoLevelsDown_IsNotOffered()
	{
		var deepest = new FilterDefinition { Conditions = [Condition(nameof(MediaQueryRow.Title), FilterOperator.Contains, "Ink")] };
		var cut = RenderBuilder(new FilterDefinition { Groups = [new FilterDefinition { Groups = [deepest] }] });

		await Assert.That(Buttons(cut, "Add group")).Count().IsEqualTo(2);
		await Assert.That(Buttons(cut, "Add condition")).Count().IsEqualTo(3);
	}

	[Test]
	public async Task AddCondition_OnTheRoot_AddsATitleCondition()
	{
		var cut = RenderBuilder(new FilterDefinition());

		await Buttons(cut, "Add condition").Single().ClickAsync(new());

		var added = await Assert.That(_changes.Last().Conditions).HasSingleItem();
		await Assert.That(added!.Field).IsEqualTo(nameof(MediaQueryRow.Title));
		await Assert.That(cut.FindAll("[aria-label='Filter condition']")).HasSingleItem();
	}

	[Test]
	public async Task RemoveCondition_WhenClicked_TakesTheConditionOut()
	{
		var cut = RenderBuilder(new FilterDefinition { Conditions = [Condition(nameof(MediaQueryRow.Title), FilterOperator.Contains, "Ink")] });

		await cut.Find("button[aria-label='Remove condition']").ClickAsync(new());

		await Assert.That(_changes.Last().Conditions).IsEmpty();
	}

	private static FilterCondition Condition(string field, FilterOperator op, object? value = null)
	{
		return new FilterCondition
		{
			Field = field,
			Operator = op,
			Value = value,
		};
	}

	private static IElement FieldTrigger(IRenderedComponent<IComponent> cut) => cut.FindAll("[aria-label='Filter condition'] [role=combobox]")[0];

	private static IElement OperatorTrigger(IRenderedComponent<IComponent> cut) => cut.FindAll("[aria-label='Filter condition'] [role=combobox]")[1];

	private static IEnumerable<IElement> Buttons(IRenderedComponent<IComponent> cut, string text) =>
		cut.FindAll("button").Where(button => button.TextContent.Trim() == text);

	private static IEnumerable<string> PickedUnits(IRenderedComponent<IComponent> cut) =>
		cut.FindAll(".number-rule-box [aria-label='Unit'] [data-state=on]").Select(unit => unit.TextContent.Trim());

	private static IEnumerable<string> OptionTexts(IRenderedComponent<IComponent> cut) =>
		cut.FindAll("[role=option]").Select(option => option.TextContent.Trim());

	private static Task TypeAsync(IRenderedComponent<IComponent> cut, double amount) =>
		cut.InvokeAsync(() => cut.FindComponents<BbNumericInput<double>>().Single().Instance.ValueChanged.InvokeAsync(amount));

	private static async Task PickAsync(IRenderedComponent<IComponent> cut, IElement trigger, string option)
	{
		await trigger.ClickAsync(new());
		await cut.FindAll("[role=option]").Single(found => found.TextContent.Trim() == option).ClickAsync(new());
	}

	private IRenderedComponent<IComponent> RenderBuilder(FilterDefinition filter)
	{
		return Render(builder =>
		{
			builder.OpenComponent<BbPortalHost>(0);
			builder.CloseComponent();
			builder.OpenComponent<FeedFilterBuilder>(1);
			builder.AddComponentParameter(2, nameof(FeedFilterBuilder.Filter), filter);
			builder.AddComponentParameter(3, nameof(FeedFilterBuilder.FilterChanged), EventCallback.Factory.Create<FilterDefinition>(this, _changes.Add));
			builder.CloseComponent();
		});
	}
}
