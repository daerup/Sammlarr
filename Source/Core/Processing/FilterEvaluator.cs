using System.Text.Json;
using Contracts.Processing;

namespace Core.Processing;

internal static class FilterEvaluator
{
	public static string Build(QueryFilter filter)
	{
		var parts = filter.Groups.Select(BuildGroup).ToList();
		return Join(filter.Operator, parts);
	}

	private static string BuildGroup(FilterGroup group)
	{
		var predicateParts = group.Predicates.Select(Compile).Where(p => p.Length > 0).Select(p => $"({p})");
		var nestedParts = group.Groups.Select(BuildGroup).Where(p => p.Length > 0);
		var allParts = predicateParts.Concat(nestedParts).ToList();

		return Join(group.Operator, allParts);
	}

	private static string Join(FilterOperator @operator, List<string> parts) =>
		parts.Count switch
		{
			0 => string.Empty,
			1 => parts[0],
			_ => $"({string.Join($" {@operator.ToString().ToLower()} ", parts)})",
		};

	private static string Compile(FilterPredicate p)
	{
		var f = p.Field;
		var v = p.Value;

		return p.Operator switch
		{
			FilterPredicateOperator.Contains => $"{f} != null && {f}.Contains({v})",
			FilterPredicateOperator.NotContains => $"{f} == null || !{f}.Contains({v})",
			FilterPredicateOperator.StartsWith => $"{f} != null && {f}.StartsWith({v})",
			FilterPredicateOperator.EndsWith => $"{f} != null && {f}.EndsWith({v})",
			FilterPredicateOperator.IsEmpty => $"{f} == null || {f} == \"\"",
			FilterPredicateOperator.IsNotEmpty => $"{f} != null && {f} != \"\"",
			FilterPredicateOperator.IsTrue => $"{f} == true",
			FilterPredicateOperator.IsFalse => $"{f} == false",
			FilterPredicateOperator.Equals => FormatEquals(f, v, p.ValueKind),
			FilterPredicateOperator.NotEquals => FormatNotEquals(f, v, p.ValueKind),
			FilterPredicateOperator.GreaterThan => FormatComparison(f, ">", v, p.ValueKind),
			FilterPredicateOperator.LessThan => FormatComparison(f, "<", v, p.ValueKind),
			FilterPredicateOperator.GreaterOrEqual => FormatComparison(f, ">=", v, p.ValueKind),
			FilterPredicateOperator.LessOrEqual => FormatComparison(f, "<=", v, p.ValueKind),
			FilterPredicateOperator.Between => p.ValueEnd is null ? string.Empty : FormatBetween(f, v, p.ValueEnd, p.ValueKind),
			_ => string.Empty,
		};
	}

	private static string FormatEquals(string field, string? value, FilterValueKind kind) =>
		kind switch
		{
			FilterValueKind.String => $"{field} != null && {field}.ToLower() == {value}.ToLower()",
			FilterValueKind.Date => $"{field}.HasValue && {field}.Value.DayNumber == {ToDayNumber(value)}",
			_ => $"{field} == {value}",
		};

	private static string FormatNotEquals(string field, string? value, FilterValueKind kind) =>
		kind switch
		{
			FilterValueKind.String => $"({field} == null || {field}.ToLower() != {value}.ToLower())",
			FilterValueKind.Date => $"(!{field}.HasValue || {field}.Value.DayNumber != {ToDayNumber(value)})",
			_ => $"{field} != {value}",
		};

	private static string FormatComparison(string field, string op, string? value, FilterValueKind kind) =>
		kind == FilterValueKind.Date ? $"{field}.HasValue && {field}.Value.DayNumber {op} {ToDayNumber(value)}" : $"{field} {op} {value}";

	private static string FormatBetween(string field, string? min, string? max, FilterValueKind kind) =>
		kind == FilterValueKind.Date
			? $"{field}.HasValue && {field}.Value.DayNumber >= {ToDayNumber(min)} && {field}.Value.DayNumber <= {ToDayNumber(max)}"
			: $"{field} >= {min} && {field} <= {max}";

	private static int ToDayNumber(string? value) => value is null ? 0 : DateOnly.FromDateTime(JsonSerializer.Deserialize<DateTime>(value)).DayNumber;
}
