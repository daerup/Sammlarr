namespace Shared;

public static class StringExtensions
{
	public static string? NullIfBlank(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
