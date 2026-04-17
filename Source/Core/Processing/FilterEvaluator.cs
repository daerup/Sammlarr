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
		var predicateParts = group.Predicates.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => $"({p})");
		var nestedParts = group.Groups.Select(BuildGroup);
		var allParts = predicateParts.Concat(nestedParts).ToList();

		return Join(group.Operator, allParts);
	}

	private static string Join(FilterOperator @operator, List<string> parts)
	{
		return parts.Count switch
		{
			0 => string.Empty,
			1 => parts[0],
			_ => $"({string.Join($" {@operator.ToString().ToLower()} ", parts)})",
		};
	}
}
