using Contracts.Processing;

namespace Core.Processing;

internal static class FilterEvaluator
{
	public static string Build(QueryFilter filter)
	{
		return filter.Groups.Count == 0 ? string.Empty : Combine(filter.Groups.Select(BuildGroup), filter.Operator);
	}

	private static string BuildGroup(FilterGroup group)
	{
		var parts = group.Predicates.Concat(group.Groups.Select(BuildGroup)).ToList();
		return Combine(parts, group.Operator);
	}

	private static string Combine(IEnumerable<string> parts, FilterOperator @operator)
	{
		var list = parts.ToList();
		var operatorString = $" {@operator.ToString().ToLower()} ";
		return list.Count switch
		{
			0 => string.Empty,
			1 => list[0],
			_ => $"({string.Join(operatorString, list)})",
		};
	}
}
