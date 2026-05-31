using System.Globalization;

namespace Integration.Tests.Infrastructure.Media;

internal static class Cells
{
	private const string NullSentinel = "<null>";

	public static bool IsNull(string? cell) => cell is not null && cell.Trim().Equals(NullSentinel, StringComparison.OrdinalIgnoreCase);

	public static string? Text(string? cell) => IsNull(cell) ? string.Empty : cell;

	public static DateTimeOffset? Date(string? cell) =>
		string.IsNullOrEmpty(cell) ? null : DateTimeOffset.Parse(cell, CultureInfo.GetCultureInfo("de-CH"), DateTimeStyles.AssumeUniversal);

	public static double? Number(string? cell) => string.IsNullOrEmpty(cell) ? null : double.Parse(cell, CultureInfo.InvariantCulture);
}
