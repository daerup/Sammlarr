using BlazorBlueprint.Primitives.Filtering;
using Domain.Processing;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Feeds.Pages.Editor;
using BbFilterOp = BlazorBlueprint.Primitives.Filtering.FilterOperator;
using BbLogicalOp = BlazorBlueprint.Primitives.Filtering.LogicalOperator;
using DomainFilterOp = Domain.Processing.FilterOperator;

namespace UI.Tests.Feeds;

public class FilterDefinitionConverterTests
{
	[Test]
	public async Task ToQueryFilter_WhenNull_ReturnsNull() => await Assert.That(FilterDefinitionConverter.ToQueryFilter(null)).IsNull();

	[Test]
	public async Task ToQueryFilter_WhenNoConditions_ReturnsNull() => await Assert.That(FilterDefinitionConverter.ToQueryFilter(new FilterDefinition())).IsNull();

	[Test]
	public async Task ToQueryFilter_WhenTextCondition_MapsFieldOperatorAndValue()
	{
		var result = FilterDefinitionConverter.ToQueryFilter(Definition(Condition("Title", BbFilterOp.Contains, "Batman")));

		var predicate = result!.Groups[0].Predicates[0];
		await Assert.That(predicate.Field).IsEqualTo("Title");
		await Assert.That(predicate.Operator).IsEqualTo(FilterPredicateOperator.Contains);
		await Assert.That(predicate.ValueKind).IsEqualTo(FilterValueKind.String);
		await Assert.That(predicate.Value).IsEqualTo("\"Batman\"");
	}

	[Test]
	[Arguments(BbLogicalOp.And, DomainFilterOp.And)]
	[Arguments(BbLogicalOp.Or, DomainFilterOp.Or)]
	public async Task ToQueryFilter_WhenLogicalOperatorSet_MapsIt(BbLogicalOp given, DomainFilterOp expected)
	{
		var definition = Definition(Condition("Title", BbFilterOp.Contains, "Batman"));
		definition.Operator = given;

		var result = FilterDefinitionConverter.ToQueryFilter(definition);

		await Assert.That(result!.Operator).IsEqualTo(expected);
		await Assert.That(result.Groups[0].Operator).IsEqualTo(expected);
	}

	[Test]
	public async Task ToQueryFilter_WhenConditionHasNoField_DropsIt() =>
		await Assert.That(FilterDefinitionConverter.ToQueryFilter(Definition(Condition(string.Empty, BbFilterOp.Contains, "Batman")))).IsNull();

	[Test]
	public async Task ToQueryFilter_WhenConditionNeedsAValueButHasNone_DropsIt() =>
		await Assert.That(FilterDefinitionConverter.ToQueryFilter(Definition(Condition("Title", BbFilterOp.Contains)))).IsNull();

	[Test]
	public async Task ToQueryFilter_WhenOperatorNeedsNoValue_KeepsTheCondition()
	{
		var result = FilterDefinitionConverter.ToQueryFilter(Definition(Condition("Title", BbFilterOp.IsEmpty)));

		await Assert.That(result!.Groups[0].Predicates).Count().IsEqualTo(1);
		await Assert.That(result.Groups[0].Predicates[0].Operator).IsEqualTo(FilterPredicateOperator.IsEmpty);
	}

	[Test]
	public async Task FromQueryFilter_WhenNull_ReturnsNull() => await Assert.That(FilterDefinitionConverter.FromQueryFilter(null)).IsNull();

	[Test]
	[Arguments("Batman")]
	[Arguments(2000.0)]
	[Arguments(true)]
	public async Task RoundTrip_WhenValueKindVaries_PreservesTheValue(object value)
	{
		var restored = RoundTrip(Definition(Condition("Title", BbFilterOp.Equals, value)));

		await Assert.That(restored!.Conditions[0].Value).IsEqualTo(value);
	}

	[Test]
	public async Task RoundTrip_WhenOperatorIsIn_PreservesTheSelectedOptions()
	{
		string[] types = ["Movie", "Show"];

		var restored = RoundTrip(Definition(Condition("MediaType", BbFilterOp.In, types)));

		await Assert.That(restored!.Conditions[0].Value as string[]).IsEquivalentTo(types);
	}

	[Test]
	public async Task ToQueryFilter_ForARelativeDate_WritesASignedDurationAsADate()
	{
		var result = FilterDefinitionConverter.ToQueryFilter(
			Definition(Condition("ReleaseDate", BbFilterOp.GreaterThan, new RelativeDate(2, DateUnit.Years, Ahead: false)))
		);

		var predicate = result!.Groups[0].Predicates[0];
		await Assert.That(predicate.ValueKind).IsEqualTo(FilterValueKind.Date);
		await Assert.That(predicate.Value).IsEqualTo("\"-P2Y\"");
	}

	[Test]
	public async Task RoundTrip_ForARelativeDate_KeepsItRelative()
	{
		var twoYearsAgo = new RelativeDate(2, DateUnit.Years, Ahead: false);

		var restored = RoundTrip(Definition(Condition("ReleaseDate", BbFilterOp.GreaterThan, twoYearsAgo)));

		await Assert.That(restored!.Conditions[0].Value).IsEqualTo(twoYearsAgo);
	}

	[Test]
	public async Task RoundTrip_ForABetweenWithAFixedAndARelativeEnd_KeepsBothKinds()
	{
		var between = Condition("ReleaseDate", BbFilterOp.Between, new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		between.ValueEnd = new RelativeDate(10, DateUnit.Years, Ahead: false);

		var restored = RoundTrip(Definition(between));

		await Assert.That(restored!.Conditions[0].Value).IsEqualTo(new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
		await Assert.That(restored.Conditions[0].ValueEnd).IsEqualTo(new RelativeDate(10, DateUnit.Years, Ahead: false));
	}

	[Test]
	[Arguments(BbFilterOp.NotIn)]
	[Arguments(BbFilterOp.Contains)]
	public async Task RoundTrip_ForGenresPickedAndTyped_KeepsBothAndTheOperator(BbFilterOp op)
	{
		var match = new GenreMatch(["Drama", "Sci-Fi & Fantasy"], ["anim"]);

		var restored = RoundTrip(Definition(Condition("Genres", op, match)));

		await Assert.That(restored!.Conditions[0].Operator).IsEqualTo(op);
		var value = await Assert.That(restored.Conditions[0].Value).IsTypeOf<GenreMatch>();
		await Assert.That(value!.Genres).IsEquivalentTo(match.Genres, CollectionOrdering.Matching);
		await Assert.That(value.Texts).IsEquivalentTo(match.Texts, CollectionOrdering.Matching);
	}

	[Test]
	public async Task ToQueryFilter_ForGenresContainingAll_WritesContainsAll()
	{
		var written = FilterDefinitionConverter.ToQueryFilter(Definition(Condition("Genres", BbFilterOp.Contains, new GenreMatch(["Drama"], []))));

		await Assert.That(written!.Groups[0].Predicates[0].Operator).IsEqualTo(FilterPredicateOperator.ContainsAll);
	}

	[Test]
	[Arguments(BbFilterOp.In)]
	[Arguments(BbFilterOp.Contains)]
	public async Task Incomplete_ForGenresWithNothingPickedOrTyped_FindsIt(BbFilterOp op)
	{
		var empty = Condition("Genres", op, GenreMatch.None);

		await Assert.That(FilterDefinitionConverter.Incomplete(Definition(empty))).IsSome().And.IsEqualTo(empty);
	}

	[Test]
	public async Task RoundTrip_WhenFieldAndOperatorSet_PreservesThem()
	{
		var restored = RoundTrip(Definition(Condition("Year", BbFilterOp.GreaterThan, 2000.0)));

		await Assert.That(restored!.Conditions[0].Field).IsEqualTo("Year");
		await Assert.That(restored.Conditions[0].Operator).IsEqualTo(BbFilterOp.GreaterThan);
	}

	[Test]
	public async Task Incomplete_ForABetweenWithoutItsEndInANestedGroup_FindsIt()
	{
		var between = new FilterCondition
		{
			Field = "Rating",
			Operator = BbFilterOp.Between,
			Value = 6.0,
		};
		var definition = Definition(Condition("Title", BbFilterOp.Contains, "Batman"));
		definition.Groups.Add(Definition(between));

		await Assert.That(FilterDefinitionConverter.Incomplete(definition)).IsSome().And.IsEqualTo(between);
	}

	[Test]
	public async Task Incomplete_ForAMediaTypeWithOnePicked_FindsNothing()
	{
		string[] picked = ["Movie"];

		await Assert.That(FilterDefinitionConverter.Incomplete(Definition(Condition("MediaType", BbFilterOp.In, picked)))).IsNone();
	}

	[Test]
	public async Task Incomplete_ForAnIsOneOfStillHoldingTheValueOfAnIs_FindsIt()
	{
		var switched = Condition("MediaType", BbFilterOp.In, "Movie");

		await Assert.That(FilterDefinitionConverter.Incomplete(Definition(switched))).IsSome().And.IsEqualTo(switched);
	}

	private static FilterDefinition? RoundTrip(FilterDefinition definition) =>
		FilterDefinitionConverter.FromQueryFilter(FilterDefinitionConverter.ToQueryFilter(definition));

	private static FilterCondition Condition(string field, BbFilterOp op, object? value = null) =>
		new()
		{
			Field = field,
			Operator = op,
			Value = value,
		};

	private static FilterDefinition Definition(params FilterCondition[] conditions) => new() { Conditions = [.. conditions] };
}
