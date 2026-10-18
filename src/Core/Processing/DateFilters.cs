using System.Text.Json;
using Domain.Processing;

namespace Core.Processing;

internal static class DateFilters
{
	public static QueryFilter On(QueryFilter filter, DateOnly today) => filter with { Groups = [.. filter.Groups.Select(group => On(group, today))] };

	private static FilterGroup On(FilterGroup group, DateOnly today)
	{
		return group with
		{
			Predicates = [.. group.Predicates.Select(predicate => On(predicate, today))],
			Groups = [.. group.Groups.Select(nested => On(nested, today))],
		};
	}

	private static FilterPredicate On(FilterPredicate predicate, DateOnly today)
	{
		return predicate.ValueKind is FilterValueKind.Date
			? predicate with
			{
				Value = On(predicate.Value, today),
				ValueEnd = On(predicate.ValueEnd, today),
			}
			: predicate;
	}

	private static string? On(string? value, DateOnly today) => Optional.Of(value).Map(json => Resolved(json, today)).ToNullable();

	private static string Resolved(string json, DateOnly today)
	{
		return Optional
			.Of(JsonSerializer.Deserialize<string>(json))
			.Map(RelativeDate.FromIso)
			.Map(relative => JsonSerializer.Serialize(relative.On(today).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)))
			.GetValueOr(json);
	}
}
