namespace UI.Shared.Filters;

public sealed record FilterChoice<TFilter>(string Label, Func<TFilter, bool> IsOn, Func<TFilter, TFilter> Pick, int? Count = null);

public sealed record FilterGroup<TFilter>(string Title, IReadOnlyList<FilterChoice<TFilter>> Choices, bool Single = false);

public sealed record FilterSearch<TFilter>(string Placeholder, Func<TFilter, string?> Text, Func<TFilter, string?, TFilter> With);

internal static class FilterChoices<TFilter>
{
	internal static FilterChoice<TFilter> Toggle<T>(
		string label,
		T value,
		Func<TFilter, IReadOnlyList<T>> chosen,
		Func<TFilter, IReadOnlyList<T>, TFilter> with,
		int? count = null
	)
	{
		return new(label, filter => chosen(filter).Contains(value), filter => with(filter, Toggled(chosen(filter), value)), count);
	}

	private static IReadOnlyList<T> Toggled<T>(IReadOnlyList<T> values, T value)
	{
		return values.Contains(value) ? [.. values.Where(item => !EqualityComparer<T>.Default.Equals(item, value))] : [.. values, value];
	}
}
