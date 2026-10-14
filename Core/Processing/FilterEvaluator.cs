using System.Text.Json;
using Domain.Processing;

namespace Core.Processing;

internal static class FilterEvaluator
{
	private static readonly HashSet<string> CollectionStringFields = [with(StringComparer.Ordinal), "Genres"];
	private static readonly HashSet<string> EnumStringFields = [with(StringComparer.Ordinal), "MediaType"];

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
			_ => $"({string.Join($" {@operator.ToString().ToLowerInvariant()} ", parts)})",
		};

	private static string Compile(FilterPredicate p)
	{
		if (CollectionStringFields.Contains(p.Field))
		{
			return CompileCollection(p);
		}

		var f = EnumStringFields.Contains(p.Field) ? $"{p.Field}.ToString()" : p.Field;
		var v = p.Value;

		return p.Operator switch
		{
			FilterPredicateOperator.Contains => $"{f} != null && {f}.ToLower().Contains({v}.ToLower())",
			FilterPredicateOperator.NotContains => $"{f} == null || !{f}.ToLower().Contains({v}.ToLower())",
			FilterPredicateOperator.StartsWith => $"{f} != null && {f}.ToLower().StartsWith({v}.ToLower())",
			FilterPredicateOperator.EndsWith => $"{f} != null && {f}.ToLower().EndsWith({v}.ToLower())",
			FilterPredicateOperator.IsEmpty => FormatIsEmpty(f, p.ValueKind),
			FilterPredicateOperator.IsNotEmpty => FormatIsNotEmpty(f, p.ValueKind),
			FilterPredicateOperator.IsTrue => $"{f} == true",
			FilterPredicateOperator.IsFalse => $"{f} == false",
			FilterPredicateOperator.Equals => FormatEquals(f, v, p.ValueKind),
			FilterPredicateOperator.NotEquals => FormatNotEquals(f, v, p.ValueKind),
			FilterPredicateOperator.GreaterThan => FormatComparison(f, ">", v, p.ValueKind),
			FilterPredicateOperator.LessThan => FormatComparison(f, "<", v, p.ValueKind),
			FilterPredicateOperator.GreaterOrEqual => FormatComparison(f, ">=", v, p.ValueKind),
			FilterPredicateOperator.LessOrEqual => FormatComparison(f, "<=", v, p.ValueKind),
			FilterPredicateOperator.Between => p.ValueEnd is null ? string.Empty : FormatBetween(f, v, p.ValueEnd, p.ValueKind),
			FilterPredicateOperator.In => FormatIn(f, v),
			FilterPredicateOperator.NotIn => FormatIn(f, v) is { Length: > 0 } expr ? $"!({expr})" : string.Empty,
			_ => string.Empty,
		};
	}

	private static string CompileCollection(FilterPredicate p)
	{
		var f = p.Field;
		var v = p.Value;

		return p.Operator switch
		{
			FilterPredicateOperator.Contains => $"{f}.Any(g => g.ToLower().Contains({v}.ToLower()))",
			FilterPredicateOperator.NotContains => $"!{f}.Any(g => g.ToLower().Contains({v}.ToLower()))",
			FilterPredicateOperator.StartsWith => $"{f}.Any(g => g.ToLower().StartsWith({v}.ToLower()))",
			FilterPredicateOperator.EndsWith => $"{f}.Any(g => g.ToLower().EndsWith({v}.ToLower()))",
			FilterPredicateOperator.Equals => $"{f}.Any(g => g.ToLower() == {v}.ToLower())",
			FilterPredicateOperator.NotEquals => $"!{f}.Any(g => g.ToLower() == {v}.ToLower())",
			FilterPredicateOperator.IsEmpty => $"{f} == null || {f}.Count == 0",
			FilterPredicateOperator.IsNotEmpty => $"{f} != null && {f}.Count > 0",
			_ => string.Empty,
		};
	}

	private static string FormatIn(string field, string? jsonArray)
	{
		if (string.IsNullOrEmpty(jsonArray))
		{
			return string.Empty;
		}
		using var doc = JsonDocument.Parse(jsonArray);
		if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
		{
			return string.Empty;
		}
		var literals = doc.RootElement.EnumerateArray().Select(v => v.GetRawText());
		return $"{field} in ({string.Join(", ", literals)})";
	}

	private static string FormatIsEmpty(string field, FilterValueKind kind) =>
		kind == FilterValueKind.String ? $"{field} == null || {field} == \"\"" : $"{field} == null";

	private static string FormatIsNotEmpty(string field, FilterValueKind kind) =>
		kind == FilterValueKind.String ? $"{field} != null && {field} != \"\"" : $"{field} != null";

	private static string FormatEquals(string field, string? value, FilterValueKind kind) =>
		kind switch
		{
			FilterValueKind.String => $"{field} != null && {field}.ToLower() == {value}.ToLower()",
			FilterValueKind.Date => FormattableString.Invariant($"{field}.HasValue && {field}.Value.DayNumber == {ToDayNumber(value)}"),
			_ => $"{field} == {value}",
		};

	private static string FormatNotEquals(string field, string? value, FilterValueKind kind) =>
		kind switch
		{
			FilterValueKind.String => $"({field} == null || {field}.ToLower() != {value}.ToLower())",
			FilterValueKind.Date => FormattableString.Invariant($"(!{field}.HasValue || {field}.Value.DayNumber != {ToDayNumber(value)})"),
			_ => $"{field} != {value}",
		};

	private static string FormatComparison(string field, string op, string? value, FilterValueKind kind) =>
		kind == FilterValueKind.Date
			? FormattableString.Invariant($"{field}.HasValue && {field}.Value.DayNumber {op} {ToDayNumber(value)}")
			: $"{field} {op} {value}";

	private static string FormatBetween(string field, string? min, string? max, FilterValueKind kind) =>
		kind == FilterValueKind.Date
			? FormattableString.Invariant($"{field}.HasValue && {field}.Value.DayNumber >= {ToDayNumber(min)} && {field}.Value.DayNumber <= {ToDayNumber(max)}")
			: $"{field} >= {min} && {field} <= {max}";

	private static int ToDayNumber(string? value) => value is null ? 0 : DateOnly.FromDateTime(JsonSerializer.Deserialize<DateTime>(value)).DayNumber;
}
