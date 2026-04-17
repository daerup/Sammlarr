using BlazorBlueprint.Primitives.Filtering;
using Contracts.Processing;
using BBFilterOp = BlazorBlueprint.Primitives.Filtering.FilterOperator;
using BBLogicalOp = BlazorBlueprint.Primitives.Filtering.LogicalOperator;
using ContractsFilterOp = Contracts.Processing.FilterOperator;

namespace UI.Features.Import.Pages.Subscription;

internal static class FilterDefinitionConverter
{
	public static QueryFilter? ToQueryFilter(FilterDefinition filter)
	{
		if (filter.IsEmpty)
			return null;

		var group = ToFilterGroup(filter);
		if (group is null)
			return null;

		return new QueryFilter { Operator = MapLogicalOperator(filter.Operator), Groups = [group] };
	}

	private static FilterGroup? ToFilterGroup(FilterDefinition filterDef)
	{
		var predicates = filterDef.Conditions.Select(BuildPredicate).Where(p => p is not null).Select(p => p!).ToList();

		var nestedGroups = filterDef.Groups.Select(ToFilterGroup).Where(g => g is not null).Select(g => g!).ToList();

		if (predicates.Count == 0 && nestedGroups.Count == 0)
			return null;

		return new FilterGroup
		{
			Operator = MapLogicalOperator(filterDef.Operator),
			Predicates = predicates,
			Groups = nestedGroups,
		};
	}

	private static ContractsFilterOp MapLogicalOperator(BBLogicalOp op) => op == BBLogicalOp.Or ? ContractsFilterOp.Or : ContractsFilterOp.And;

	private static string? BuildPredicate(FilterCondition condition)
	{
		if (string.IsNullOrWhiteSpace(condition.Field))
			return null;

		// Valueless operators don't need a value check
		var valueless = condition.Operator is BBFilterOp.IsEmpty or BBFilterOp.IsNotEmpty or BBFilterOp.IsTrue or BBFilterOp.IsFalse;

		if (!valueless && condition.Value is null)
			return null;

		var f = condition.Field;
		var v = condition.Value;
		var v2 = condition.ValueEnd;

		return condition.Operator switch
		{
			BBFilterOp.Equals => FormatEquals(f, v!),
			BBFilterOp.NotEquals => FormatNotEquals(f, v!),
			BBFilterOp.Contains => $"{f} != null && {f}.Contains(\"{Escape(v!)}\")",
			BBFilterOp.NotContains => $"{f} == null || !{f}.Contains(\"{Escape(v!)}\")",
			BBFilterOp.StartsWith => $"{f} != null && {f}.StartsWith(\"{Escape(v!)}\")",
			BBFilterOp.EndsWith => $"{f} != null && {f}.EndsWith(\"{Escape(v!)}\")",
			BBFilterOp.IsEmpty => $"{f} == null || {f} == \"\"",
			BBFilterOp.IsNotEmpty => $"{f} != null && {f} != \"\"",
			BBFilterOp.GreaterThan => FormatComparison(f, ">", v!),
			BBFilterOp.LessThan => FormatComparison(f, "<", v!),
			BBFilterOp.GreaterOrEqual => FormatComparison(f, ">=", v!),
			BBFilterOp.LessOrEqual => FormatComparison(f, "<=", v!),
			BBFilterOp.Between => v2 is null ? null : FormatBetween(f, v!, v2),
			BBFilterOp.IsTrue => $"{f} == true",
			BBFilterOp.IsFalse => $"{f} == false",
			_ => null, // InLast, InNext, In, NotIn, DateIs, DateIsNot — not supported via Dynamic LINQ inline predicates
		};
	}

	private static string FormatEquals(string field, object value) =>
		value is string s ? $"{field} != null && {field}.ToLower() == \"{Escape(s).ToLower()}\""
		: IsDate(value) ? $"{field}.HasValue && {field}.Value.DayNumber == {ToDayNumber(value)}"
		: $"{field} == {FormatValue(value)}";

	private static string FormatNotEquals(string field, object value) =>
		value is string s ? $"({field} == null || {field}.ToLower() != \"{Escape(s).ToLower()}\")"
		: IsDate(value) ? $"(!{field}.HasValue || {field}.Value.DayNumber != {ToDayNumber(value)})"
		: $"{field} != {FormatValue(value)}";

	private static string FormatComparison(string field, string op, object value) =>
		IsDate(value) ? $"{field}.HasValue && {field}.Value.DayNumber {op} {ToDayNumber(value)}" : $"{field} {op} {FormatValue(value)}";

	private static string FormatBetween(string field, object min, object max) =>
		IsDate(min)
			? $"{field}.HasValue && {field}.Value.DayNumber >= {ToDayNumber(min)} && {field}.Value.DayNumber <= {ToDayNumber(max)}"
			: $"{field} >= {FormatValue(min)} && {field} <= {FormatValue(max)}";

	private static bool IsDate(object value) => value is DateOnly or DateTime;

	private static int ToDayNumber(object value) =>
		value switch
		{
			DateOnly d => d.DayNumber,
			DateTime dt => DateOnly.FromDateTime(dt).DayNumber,
			_ => 0,
		};

	private static string FormatValue(object value) =>
		value switch
		{
			string s => $"\"{Escape(s)}\"",
			bool b => b.ToString().ToLower(),
			_ => value.ToString()!,
		};

	private static string Escape(object value) => value.ToString()?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? string.Empty;
}
