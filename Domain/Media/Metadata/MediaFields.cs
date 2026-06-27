using System.Diagnostics.CodeAnalysis;

namespace Domain.Media.Metadata;

internal static class MediaFields
{
	public static bool HasText([NotNullWhen(true)] string? value) => !string.IsNullOrWhiteSpace(value);

	public static bool HasItems<T>(ValueSet<T> items) => items.Count > 0;

	public static bool HasDuration([NotNullWhen(true)] TimeSpan? value) => value is { Ticks: > 0 };

	public static bool HasDate([NotNullWhen(true)] DateOnly? value) => value is { } date && date > DateOnly.MinValue;

	public static bool HasCount([NotNullWhen(true)] int? value) => value is > 0;

	public static ValueSet<T> Coalesce<T>(ValueSet<T> primary, ValueSet<T> secondary) => primary.Count > 0 ? primary : secondary;

	public static RatingSummary Coalesce(RatingSummary primary, RatingSummary secondary) => primary.Sources.Count > 0 ? primary : secondary;
}
