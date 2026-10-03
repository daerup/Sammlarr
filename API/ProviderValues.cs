using System.Globalization;

namespace API;

internal static class ProviderValues
{
	public static string? Text(string? value) => value.NullIfBlank();

	public static DateOnly? Date(string? value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var date) ? date : null;

	public static DateOnly? YearStart(int? year) => year is > 0 ? new DateOnly(year.Value, 1, 1) : null;

	public static int? Count(int? value) => value is > 0 ? value : null;

	public static TimeSpan? Minutes(int? value) => value is > 0 ? TimeSpan.FromMinutes(value.Value) : null;

	public static TimeSpan? Milliseconds(long? value) => value is > 0 ? TimeSpan.FromMilliseconds(value.Value) : null;

	public static ValueSet<string> Genres(IEnumerable<string?>? names)
	{
		return [.. (names ?? []).Select(Text).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];
	}
}
