using System.Text.Json;
using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using BBFilterOp = BlazorBlueprint.Primitives.Filtering.FilterOperator;
using BBLogicalOp = BlazorBlueprint.Primitives.Filtering.LogicalOperator;
using ContractsFilterOp = Contracts.Processing.FilterOperator;

namespace UI.Features.Feeds.Pages.Editor;

internal static class FilterDefinitionConverter
{
	public static QueryFilter? ToQueryFilter(FilterDefinition? filter)
	{
		if (filter is null || filter.IsEmpty)
		{
			return null;
		}

		var group = ToFilterGroup(filter);
		if (group is null)
		{
			return null;
		}

		return new QueryFilter { Operator = ToContractsOperator(filter.Operator), Groups = [group] };
	}

	public static FilterDefinition? FromQueryFilter(QueryFilter? filter)
	{
		if (filter is null)
		{
			return null;
		}

		var definition = new FilterDefinition { Operator = ToBbOperator(filter.Operator) };

		if (filter.Groups.Count == 1)
		{
			PopulateDefinition(definition, filter.Groups[0]);
		}
		else
		{
			foreach (var group in filter.Groups)
			{
				definition.Groups.Add(ToDefinition(group));
			}
		}

		return definition.IsEmpty ? null : definition;
	}

	private static FilterGroup? ToFilterGroup(FilterDefinition filterDef)
	{
		var predicates = filterDef.Conditions.Select(ToPredicate).Where(p => p is not null).Select(p => p!).ToList();
		var nestedGroups = filterDef.Groups.Select(ToFilterGroup).Where(g => g is not null).Select(g => g!).ToList();

		if (predicates.Count == 0 && nestedGroups.Count == 0)
		{
			return null;
		}

		return new FilterGroup
		{
			Operator = ToContractsOperator(filterDef.Operator),
			Predicates = predicates,
			Groups = nestedGroups,
		};
	}

	private static FilterDefinition ToDefinition(FilterGroup group)
	{
		var definition = new FilterDefinition { Operator = ToBbOperator(group.Operator) };
		PopulateDefinition(definition, group);
		return definition;
	}

	private static void PopulateDefinition(FilterDefinition definition, FilterGroup group)
	{
		foreach (var predicate in group.Predicates)
		{
			definition.Conditions.Add(ToCondition(predicate));
		}

		foreach (var nested in group.Groups)
		{
			definition.Groups.Add(ToDefinition(nested));
		}
	}

	private static FilterPredicate? ToPredicate(FilterCondition condition)
	{
		if (string.IsNullOrWhiteSpace(condition.Field))
		{
			return null;
		}

		var valueless = condition.Operator is BBFilterOp.IsEmpty or BBFilterOp.IsNotEmpty or BBFilterOp.IsTrue or BBFilterOp.IsFalse;
		if (!valueless && condition.Value is null)
		{
			return null;
		}

		return new FilterPredicate
		{
			Field = condition.Field,
			Operator = ToPredicateOperator(condition.Operator),
			ValueKind = DetectKind(condition.Value ?? condition.ValueEnd),
			Value = Serialize(condition.Value),
			ValueEnd = Serialize(condition.ValueEnd),
		};
	}

	private static FilterCondition ToCondition(FilterPredicate predicate) =>
		new()
		{
			Field = predicate.Field,
			Operator = ToBbPredicateOperator(predicate.Operator),
			Value = DeserializeValue(predicate.Value, predicate.ValueKind, predicate.Operator),
			ValueEnd = Deserialize(predicate.ValueEnd, predicate.ValueKind),
		};

	private static object? DeserializeValue(string? value, FilterValueKind kind, FilterPredicateOperator op) =>
		op is FilterPredicateOperator.In or FilterPredicateOperator.NotIn
			? value is null
				? null
				: JsonSerializer.Deserialize<string[]>(value)
			: Deserialize(value, kind);

	private static ContractsFilterOp ToContractsOperator(BBLogicalOp op) => op == BBLogicalOp.Or ? ContractsFilterOp.Or : ContractsFilterOp.And;

	private static BBLogicalOp ToBbOperator(ContractsFilterOp op) => op == ContractsFilterOp.Or ? BBLogicalOp.Or : BBLogicalOp.And;

	private static FilterPredicateOperator ToPredicateOperator(BBFilterOp op) =>
		op switch
		{
			BBFilterOp.Equals => FilterPredicateOperator.Equals,
			BBFilterOp.NotEquals => FilterPredicateOperator.NotEquals,
			BBFilterOp.Contains => FilterPredicateOperator.Contains,
			BBFilterOp.NotContains => FilterPredicateOperator.NotContains,
			BBFilterOp.StartsWith => FilterPredicateOperator.StartsWith,
			BBFilterOp.EndsWith => FilterPredicateOperator.EndsWith,
			BBFilterOp.IsEmpty => FilterPredicateOperator.IsEmpty,
			BBFilterOp.IsNotEmpty => FilterPredicateOperator.IsNotEmpty,
			BBFilterOp.GreaterThan => FilterPredicateOperator.GreaterThan,
			BBFilterOp.LessThan => FilterPredicateOperator.LessThan,
			BBFilterOp.GreaterOrEqual => FilterPredicateOperator.GreaterOrEqual,
			BBFilterOp.LessOrEqual => FilterPredicateOperator.LessOrEqual,
			BBFilterOp.Between => FilterPredicateOperator.Between,
			BBFilterOp.IsTrue => FilterPredicateOperator.IsTrue,
			BBFilterOp.IsFalse => FilterPredicateOperator.IsFalse,
			BBFilterOp.In => FilterPredicateOperator.In,
			BBFilterOp.NotIn => FilterPredicateOperator.NotIn,
			BBFilterOp.DateIs => FilterPredicateOperator.Equals,
			BBFilterOp.DateIsNot => FilterPredicateOperator.NotEquals,
			BBFilterOp.InLast => FilterPredicateOperator.GreaterOrEqual,
			BBFilterOp.InNext => FilterPredicateOperator.LessOrEqual,
		};

	private static BBFilterOp ToBbPredicateOperator(FilterPredicateOperator op) =>
		op switch
		{
			FilterPredicateOperator.Equals => BBFilterOp.Equals,
			FilterPredicateOperator.NotEquals => BBFilterOp.NotEquals,
			FilterPredicateOperator.Contains => BBFilterOp.Contains,
			FilterPredicateOperator.NotContains => BBFilterOp.NotContains,
			FilterPredicateOperator.StartsWith => BBFilterOp.StartsWith,
			FilterPredicateOperator.EndsWith => BBFilterOp.EndsWith,
			FilterPredicateOperator.IsEmpty => BBFilterOp.IsEmpty,
			FilterPredicateOperator.IsNotEmpty => BBFilterOp.IsNotEmpty,
			FilterPredicateOperator.GreaterThan => BBFilterOp.GreaterThan,
			FilterPredicateOperator.LessThan => BBFilterOp.LessThan,
			FilterPredicateOperator.GreaterOrEqual => BBFilterOp.GreaterOrEqual,
			FilterPredicateOperator.LessOrEqual => BBFilterOp.LessOrEqual,
			FilterPredicateOperator.Between => BBFilterOp.Between,
			FilterPredicateOperator.IsTrue => BBFilterOp.IsTrue,
			FilterPredicateOperator.IsFalse => BBFilterOp.IsFalse,
			FilterPredicateOperator.In => BBFilterOp.In,
			FilterPredicateOperator.NotIn => BBFilterOp.NotIn,
		};

	private static FilterValueKind DetectKind(object? value) =>
		value switch
		{
			null => FilterValueKind.String,
			DateOnly or DateTime or DateTimeOffset => FilterValueKind.Date,
			bool => FilterValueKind.Boolean,
			IConvertible c
				when c.GetTypeCode()
					is TypeCode.Byte
						or TypeCode.SByte
						or TypeCode.Int16
						or TypeCode.UInt16
						or TypeCode.Int32
						or TypeCode.UInt32
						or TypeCode.Int64
						or TypeCode.UInt64
						or TypeCode.Single
						or TypeCode.Double
						or TypeCode.Decimal => FilterValueKind.Number,
			_ => FilterValueKind.String,
		};

	private static string? Serialize(object? value) =>
		value switch
		{
			null => null,
			DateOnly d => JsonSerializer.Serialize(d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
			_ => JsonSerializer.Serialize(value),
		};

	private static object? Deserialize(string? value, FilterValueKind kind) =>
		value is null
			? null
			: kind switch
			{
				FilterValueKind.Number => JsonSerializer.Deserialize<double>(value),
				FilterValueKind.Boolean => JsonSerializer.Deserialize<bool>(value),
				FilterValueKind.Date => JsonSerializer.Deserialize<DateTime>(value),
				_ => JsonSerializer.Deserialize<string>(value),
			};
}
