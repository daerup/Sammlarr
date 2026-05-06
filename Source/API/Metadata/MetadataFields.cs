namespace API.Metadata;

internal static class MetadataFields
{
	public static bool HasText(string value) => !string.IsNullOrWhiteSpace(value);

	public static bool HasItems<T>(IReadOnlyCollection<T> items) => items.Count > 0;

	public static bool HasDuration(TimeSpan value) => value > TimeSpan.Zero;

	public static bool HasDate(DateOnly value) => value > DateOnly.MinValue;

	public static bool HasCount(int value) => value > 0;

	public static string Coalesce(string primary, string secondary) => HasText(primary) ? primary : secondary;

	public static IReadOnlyList<T> Coalesce<T>(IReadOnlyList<T> primary, IReadOnlyList<T> secondary) => HasItems(primary) ? primary : secondary;

	public static TimeSpan Coalesce(TimeSpan primary, TimeSpan secondary) => HasDuration(primary) ? primary : secondary;

	public static DateOnly Coalesce(DateOnly primary, DateOnly secondary) => HasDate(primary) ? primary : secondary;

	public static int Coalesce(int primary, int secondary) => HasCount(primary) ? primary : secondary;
}
