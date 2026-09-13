using BlazorBlueprint.Primitives.Filtering;
using Domain.Processing;
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

		await Assert.That(result!.Groups[0].Predicates).HasCount(1);
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
		string[] genres = ["Action", "Drama"];

		var restored = RoundTrip(Definition(Condition("Genres", BbFilterOp.In, genres)));

		await Assert.That(restored!.Conditions[0].Value as string[]).IsEquivalentTo(genres);
	}

	[Test]
	public async Task RoundTrip_WhenFieldAndOperatorSet_PreservesThem()
	{
		var restored = RoundTrip(Definition(Condition("Year", BbFilterOp.GreaterThan, 2000.0)));

		await Assert.That(restored!.Conditions[0].Field).IsEqualTo("Year");
		await Assert.That(restored.Conditions[0].Operator).IsEqualTo(BbFilterOp.GreaterThan);
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
