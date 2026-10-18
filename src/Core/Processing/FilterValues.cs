using System.Text.Json;
using Domain.Processing;

namespace Core.Processing;

internal static class FilterValues
{
	public static QueryFilter Resolve(QueryFilter filter, DateOnly today) => filter with { Groups = [.. filter.Groups.Select(group => Resolve(group, today))] };

	private static FilterGroup Resolve(FilterGroup group, DateOnly today)
	{
		return group with
		{
			Predicates = [.. group.Predicates.Select(predicate => Resolve(predicate, today))],
			Groups = [.. group.Groups.Select(nested => Resolve(nested, today))],
		};
	}

	private static FilterPredicate Resolve(FilterPredicate predicate, DateOnly today)
	{
		return predicate.ValueKind switch
		{
			FilterValueKind.Date => predicate with
			{
				Value = Mapped(predicate.Value, json => Day(json, today)),
				ValueEnd = Mapped(predicate.ValueEnd, json => Day(json, today)),
			},
			FilterValueKind.Duration => predicate with
			{
				ValueKind = FilterValueKind.Number,
				Value = Mapped(predicate.Value, Minutes),
				ValueEnd = Mapped(predicate.ValueEnd, Minutes),
			},
			FilterValueKind.String or FilterValueKind.Number or FilterValueKind.Boolean => predicate,
		};
	}

	private static string? Mapped(string? value, Func<string, string> resolve) => Optional.Of(value).Map(resolve).ToNullable();

	private static string Day(string json, DateOnly today)
	{
		return Optional
			.Of(JsonSerializer.Deserialize<string>(json))
			.Map(RelativeDate.FromIso)
			.Map(relative => JsonSerializer.Serialize(relative.On(today).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)))
			.GetValueOr(json);
	}

	private static string Minutes(string json)
	{
		return Optional
			.Of(JsonSerializer.Deserialize<string>(json))
			.Map(Duration.FromIso)
			.Map(duration => JsonSerializer.Serialize((int)Math.Round(duration.Minutes)))
			.GetValueOr(json);
	}
}
