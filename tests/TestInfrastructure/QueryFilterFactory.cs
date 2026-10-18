using System.Text.Json;
using Domain.Processing;

namespace TestInfrastructure;

public static class QueryFilterFactory
{
	public static FilterPredicate String(string field, FilterPredicateOperator op, string value) =>
		new()
		{
			Field = field,
			Operator = op,
			ValueKind = FilterValueKind.String,
			Value = JsonSerializer.Serialize(value),
		};

	public static FilterPredicate Number(string field, FilterPredicateOperator op, double value) =>
		new()
		{
			Field = field,
			Operator = op,
			ValueKind = FilterValueKind.Number,
			Value = JsonSerializer.Serialize(value),
		};

	public static FilterPredicate Date(string field, FilterPredicateOperator op, DateOnly value) =>
		new()
		{
			Field = field,
			Operator = op,
			ValueKind = FilterValueKind.Date,
			Value = JsonSerializer.Serialize(value),
		};

	public static FilterPredicate Date(string field, FilterPredicateOperator op, RelativeDate value) =>
		new()
		{
			Field = field,
			Operator = op,
			ValueKind = FilterValueKind.Date,
			Value = JsonSerializer.Serialize(value.ToIso()),
		};

	public static FilterPredicate In(string field, params string[] values) =>
		new()
		{
			Field = field,
			Operator = FilterPredicateOperator.In,
			ValueKind = FilterValueKind.String,
			Value = JsonSerializer.Serialize(values),
		};

	public static FilterPredicate NotIn(string field, params string[] values) =>
		new()
		{
			Field = field,
			Operator = FilterPredicateOperator.NotIn,
			ValueKind = FilterValueKind.String,
			Value = JsonSerializer.Serialize(values),
		};

	public static FilterGroup Group(params FilterPredicate[] predicates) => new() { Operator = FilterOperator.And, Predicates = [.. predicates] };

	public static FilterGroup Group(FilterOperator op, params FilterPredicate[] predicates) => new() { Operator = op, Predicates = [.. predicates] };

	public static FilterGroup Group(FilterOperator op, FilterPredicate[] predicates, FilterGroup[] nestedGroups) =>
		new()
		{
			Operator = op,
			Predicates = [.. predicates],
			Groups = [.. nestedGroups],
		};

	public static QueryFilter Filter(params FilterGroup[] groups) => new() { Operator = FilterOperator.And, Groups = [.. groups] };

	public static QueryFilter Filter(FilterOperator op, params FilterGroup[] groups) => new() { Operator = op, Groups = [.. groups] };
}
