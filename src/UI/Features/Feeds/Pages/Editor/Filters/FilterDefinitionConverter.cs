using System.Text.Json;
using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using Domain.Processing;
using BBFilterOp = BlazorBlueprint.Primitives.Filtering.FilterOperator;
using BBLogicalOp = BlazorBlueprint.Primitives.Filtering.LogicalOperator;
using ContractsFilterOp = Domain.Processing.FilterOperator;

namespace UI.Features.Feeds.Pages.Editor.Filters;

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

	public static Optional<FilterCondition> Incomplete(FilterDefinition? filter)
	{
		return Optional
			.Of(filter)
			.Map(definition => definition.Conditions.FirstOrNone(IsIncomplete).Or(() => definition.Groups.Select(Incomplete).WhereSome().FirstOrNone()));
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

	private static bool IsIncomplete(FilterCondition condition)
	{
		return (FeedFields.TakesPicks(condition) && !HasPicks(condition.Value))
			|| (condition.Operator is BBFilterOp.Between && (condition.Value is null || condition.ValueEnd is null));
	}

	private static bool HasPicks(object? value)
	{
		return value switch
		{
			GenreMatch match => !match.IsEmpty,
			IEnumerable<string> picked => picked.Any(),
			_ => false,
		};
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
			Operator = PredicateOperatorOf(condition),
			ValueKind = DetectKind(condition.Value ?? condition.ValueEnd),
			Value = Serialize(condition.Value),
			ValueEnd = Serialize(condition.ValueEnd),
		};
	}

	public static FilterCondition ToCondition(FilterPredicate predicate) =>
		new()
		{
			Field = predicate.Field,
			Operator = ToBbPredicateOperator(predicate.Operator),
			Value = DeserializeValue(predicate),
			ValueEnd = Deserialize(predicate.ValueEnd, predicate.ValueKind),
		};

	private static object? DeserializeValue(FilterPredicate predicate)
	{
		return (predicate.Value, predicate.Operator, predicate.Field) switch
		{
			({ } json, FilterPredicateOperator.In or FilterPredicateOperator.NotIn or FilterPredicateOperator.ContainsAll, nameof(MediaQueryRow.Genres)) =>
				JsonSerializer.Deserialize<GenreMatch>(json),
			({ } json, FilterPredicateOperator.In or FilterPredicateOperator.NotIn, _) => JsonSerializer.Deserialize<string[]>(json),
			_ => Deserialize(predicate.Value, predicate.ValueKind),
		};
	}

	private static ContractsFilterOp ToContractsOperator(BBLogicalOp op) => op == BBLogicalOp.Or ? ContractsFilterOp.Or : ContractsFilterOp.And;

	private static BBLogicalOp ToBbOperator(ContractsFilterOp op) => op == ContractsFilterOp.Or ? BBLogicalOp.Or : BBLogicalOp.And;

	private static FilterPredicateOperator PredicateOperatorOf(FilterCondition condition)
	{
		return condition is { Field: nameof(MediaQueryRow.Genres), Operator: BBFilterOp.Contains }
			? FilterPredicateOperator.ContainsAll
			: ToPredicateOperator(condition.Operator);
	}

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
			FilterPredicateOperator.ContainsAll => BBFilterOp.Contains,
		};

	private static FilterValueKind DetectKind(object? value) =>
		value switch
		{
			null => FilterValueKind.String,
			DateOnly or DateTime or DateTimeOffset or RelativeDate => FilterValueKind.Date,
			Duration => FilterValueKind.Duration,
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
			RelativeDate relative => JsonSerializer.Serialize(relative.ToIso()),
			Duration duration => JsonSerializer.Serialize(duration.ToIso()),
			_ => JsonSerializer.Serialize(value),
		};

	private static object? Deserialize(string? value, FilterValueKind kind) =>
		value is null
			? null
			: kind switch
			{
				FilterValueKind.Number => JsonSerializer.Deserialize<double>(value),
				FilterValueKind.Boolean => JsonSerializer.Deserialize<bool>(value),
				FilterValueKind.Date => DateOf(value),
				FilterValueKind.Duration => Optional.Of(JsonSerializer.Deserialize<string>(value)).Map(Duration.FromIso).ToNullable(),
				_ => JsonSerializer.Deserialize<string>(value),
			};

	private static object DateOf(string value)
	{
		return Optional
			.Of(JsonSerializer.Deserialize<string>(value))
			.Map(RelativeDate.FromIso)
			.Match(relative => (object)relative, () => JsonSerializer.Deserialize<DateTime>(value));
	}
}
