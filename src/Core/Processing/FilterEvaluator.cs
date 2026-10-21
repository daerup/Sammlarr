using System.Text.Json;
using Domain.Processing;

namespace Core.Processing;

internal sealed record FilterExpression(string Text, IReadOnlyList<object?> Values)
{
	public bool Equals(FilterExpression? other)
	{
		return other is not null && string.Equals(Text, other.Text, StringComparison.Ordinal) && Values.SequenceEqual(other.Values);
	}

	public override int GetHashCode() => Text.GetHashCode(StringComparison.Ordinal);
}

internal static class FilterEvaluator
{
	private static readonly HashSet<string> CollectionStringFields = new(StringComparer.Ordinal) { "Genres" };
	private static readonly HashSet<string> EnumStringFields = new(StringComparer.Ordinal) { "MediaType" };
	private static readonly HashSet<string> ShowOnlyFields = new(StringComparer.Ordinal) { "Seasons", "Episodes" };

	public static FilterExpression Build(QueryFilter filter)
	{
		List<object?> values = [];
		List<string> parts = [.. filter.Groups.Select(group => BuildGroup(group, values)).Where(part => part.Length > 0)];
		return new FilterExpression(Join(filter.Operator, parts) is { Length: > 0 } expression ? expression : "true", values);
	}

	public static Optional<FilterExpression> Predicate(FilterPredicate predicate)
	{
		List<object?> values = [];
		var text = Compile(predicate, values);
		return Optional.When(text.Length > 0, () => new FilterExpression(text, values));
	}

	private static string BuildGroup(FilterGroup group, List<object?> values)
	{
		var predicateParts = group.Predicates.Select(p => Compile(p, values)).Where(p => p.Length > 0).Select(p => $"({p})");
		var nestedParts = group.Groups.Select(nested => BuildGroup(nested, values)).Where(p => p.Length > 0);
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

	private static string Compile(FilterPredicate p, List<object?> values)
	{
		var compiled = CompileField(p, values);
		return ShowOnlyFields.Contains(p.Field) && compiled.Length > 0 ? $"{p.Field} != null && ({compiled})" : compiled;
	}

	private static string CompileField(FilterPredicate p, List<object?> values)
	{
		if (CollectionStringFields.Contains(p.Field))
		{
			return CompileCollection(p, values);
		}

		var f = EnumStringFields.Contains(p.Field) ? $"{p.Field}.ToString()" : p.Field;

		return p.Operator switch
		{
			FilterPredicateOperator.Contains => $"{f} != null && {f}.ToLower().Contains({Text(p.Value, values)}.ToLower())",
			FilterPredicateOperator.NotContains => $"{f} == null || !{f}.ToLower().Contains({Text(p.Value, values)}.ToLower())",
			FilterPredicateOperator.StartsWith => $"{f} != null && {f}.ToLower().StartsWith({Text(p.Value, values)}.ToLower())",
			FilterPredicateOperator.EndsWith => $"{f} != null && {f}.ToLower().EndsWith({Text(p.Value, values)}.ToLower())",
			FilterPredicateOperator.IsEmpty => FormatIsEmpty(f, p.ValueKind),
			FilterPredicateOperator.IsNotEmpty => FormatIsNotEmpty(f, p.ValueKind),
			FilterPredicateOperator.IsTrue => $"{f} == true",
			FilterPredicateOperator.IsFalse => $"{f} == false",
			FilterPredicateOperator.Equals => FormatEquals(f, p.Value, p.ValueKind, values),
			FilterPredicateOperator.NotEquals => FormatNotEquals(f, p.Value, p.ValueKind, values),
			FilterPredicateOperator.GreaterThan => FormatComparison(f, ">", p.Value, p.ValueKind, values),
			FilterPredicateOperator.LessThan => FormatComparison(f, "<", p.Value, p.ValueKind, values),
			FilterPredicateOperator.GreaterOrEqual => FormatComparison(f, ">=", p.Value, p.ValueKind, values),
			FilterPredicateOperator.LessOrEqual => FormatComparison(f, "<=", p.Value, p.ValueKind, values),
			FilterPredicateOperator.Between => p.ValueEnd is null ? string.Empty : FormatBetween(f, p.Value, p.ValueEnd, p.ValueKind, values),
			FilterPredicateOperator.In => FormatIn(f, p.Value, values),
			FilterPredicateOperator.NotIn => FormatIn(f, p.Value, values) is { Length: > 0 } expr ? $"!({expr})" : string.Empty,
			FilterPredicateOperator.ContainsAll => string.Empty,
		};
	}

	private static string CompileCollection(FilterPredicate p, List<object?> values)
	{
		var f = p.Field;

		return p.Operator switch
		{
			FilterPredicateOperator.Contains => $"{f}.Any(g => g.ToLower().Contains({Text(p.Value, values)}.ToLower()))",
			FilterPredicateOperator.NotContains => $"!{f}.Any(g => g.ToLower().Contains({Text(p.Value, values)}.ToLower()))",
			FilterPredicateOperator.StartsWith => $"{f}.Any(g => g.ToLower().StartsWith({Text(p.Value, values)}.ToLower()))",
			FilterPredicateOperator.EndsWith => $"{f}.Any(g => g.ToLower().EndsWith({Text(p.Value, values)}.ToLower()))",
			FilterPredicateOperator.Equals => $"{f}.Any(g => g.ToLower() == {Text(p.Value, values)}.ToLower())",
			FilterPredicateOperator.NotEquals => $"!{f}.Any(g => g.ToLower() == {Text(p.Value, values)}.ToLower())",
			FilterPredicateOperator.IsEmpty => $"{f} == null || {f}.Count == 0",
			FilterPredicateOperator.IsNotEmpty => $"{f} != null && {f}.Count > 0",
			FilterPredicateOperator.In => FormatAnyGenre(f, p.Value, values),
			FilterPredicateOperator.NotIn => FormatAnyGenre(f, p.Value, values) is { Length: > 0 } expr ? $"!({expr})" : string.Empty,
			FilterPredicateOperator.ContainsAll => FormatAllGenres(f, p.Value, values),
			_ => string.Empty,
		};
	}

	private static string FormatAnyGenre(string field, string? json, List<object?> values)
	{
		var tests = GenreTests(json, values);
		return tests.Count == 0 ? string.Empty : $"{field}.Any(g => {string.Join(" || ", tests)})";
	}

	private static string FormatAllGenres(string field, string? json, List<object?> values)
	{
		return string.Join(" && ", GenreTests(json, values).Select(test => $"{field}.Any(g => {test})"));
	}

	private static List<string> GenreTests(string? json, List<object?> values)
	{
		var match = Optional.Of(json).Map(text => JsonSerializer.Deserialize<GenreMatch>(text) ?? GenreMatch.None).GetValueOr(GenreMatch.None);
		return
		[
			.. match.Genres.Select(genre => $"g.ToLower() == {Parameter(genre, values)}.ToLower()"),
			.. match.Texts.Select(text => $"g.ToLower().Contains({Parameter(text, values)}.ToLower())"),
		];
	}

	private static string FormatIn(string field, string? jsonArray, List<object?> values)
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
		var members = doc.RootElement.EnumerateArray().ToList();
		var operand = members.Any(member => member.ValueKind == JsonValueKind.Number) ? Operand(field, FilterValueKind.Number) : field;
		var parameters = members.Select(member => Parameter(Literal(member), values)).ToList();
		return $"{operand} in ({string.Join(", ", parameters)})";
	}

	private static string FormatIsEmpty(string field, FilterValueKind kind) =>
		kind == FilterValueKind.String ? $"{field} == null || {field} == \"\"" : $"{field} == null";

	private static string FormatIsNotEmpty(string field, FilterValueKind kind) =>
		kind == FilterValueKind.String ? $"{field} != null && {field} != \"\"" : $"{field} != null";

	private static string FormatEquals(string field, string? value, FilterValueKind kind, List<object?> values)
	{
		return kind switch
		{
			FilterValueKind.String => $"{field} != null && {field}.ToLower() == {Text(value, values)}.ToLower()",
			FilterValueKind.Date => $"{field}.HasValue && {field}.Value.DayNumber == {Day(value, values)}",
			_ => $"{Operand(field, kind)} == {Value(value, values)}",
		};
	}

	private static string FormatNotEquals(string field, string? value, FilterValueKind kind, List<object?> values)
	{
		return kind switch
		{
			FilterValueKind.String => $"({field} == null || {field}.ToLower() != {Text(value, values)}.ToLower())",
			FilterValueKind.Date => $"(!{field}.HasValue || {field}.Value.DayNumber != {Day(value, values)})",
			_ => $"{Operand(field, kind)} != {Value(value, values)}",
		};
	}

	private static string FormatComparison(string field, string op, string? value, FilterValueKind kind, List<object?> values)
	{
		return kind == FilterValueKind.Date
			? $"{field}.HasValue && {field}.Value.DayNumber {op} {Day(value, values)}"
			: $"{Operand(field, kind)} {op} {Value(value, values)}";
	}

	private static string FormatBetween(string field, string? min, string max, FilterValueKind kind, List<object?> values)
	{
		return kind == FilterValueKind.Date
			? $"{field}.HasValue && {field}.Value.DayNumber >= {Day(min, values)} && {field}.Value.DayNumber <= {Day(max, values)}"
			: $"{Operand(field, kind)} >= {Value(min, values)} && {Operand(field, kind)} <= {Value(max, values)}";
	}

	private static string Text(string? json, List<object?> values) => Parameter(json is null ? null : JsonSerializer.Deserialize<string>(json), values);

	private static string Value(string? json, List<object?> values)
	{
		if (json is null)
		{
			return Parameter(null, values);
		}

		using var doc = JsonDocument.Parse(json);
		return Parameter(Literal(doc.RootElement), values);
	}

	private static string Day(string? json, List<object?> values)
	{
		return Parameter(json is null ? 0 : DateOnly.FromDateTime(JsonSerializer.Deserialize<DateTime>(json)).DayNumber, values);
	}

	private static object? Literal(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Number => element.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Undefined or JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null => null,
		};
	}

	private static string Operand(string field, FilterValueKind kind) => kind == FilterValueKind.Number ? $"Double?({field})" : field;

	private static string Parameter(object? value, List<object?> values)
	{
		values.Add(value);
		return $"@{values.Count - 1}";
	}
}
