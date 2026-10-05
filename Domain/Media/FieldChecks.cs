using System.Diagnostics.CodeAnalysis;

namespace Domain.Media;

internal static class FieldChecks
{
	public static bool HasText([NotNullWhen(true)] string? value) => value is not null;

	public static bool HasItems<T>(ValueSet<T> items) => items.Count > 0;

	public static bool HasDuration([NotNullWhen(true)] TimeSpan? value) => value is not null;

	public static bool HasDate([NotNullWhen(true)] DateOnly? value) => value is not null;

	public static bool HasCount([NotNullWhen(true)] int? value) => value is not null;

	public static ValueSet<T> Coalesce<T>(ValueSet<T> primary, ValueSet<T> secondary) => primary.Count > 0 ? primary : secondary;
}
