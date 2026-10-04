using System.Globalization;

namespace UI.Features.Settings.Components.Users;

internal static class TypedName
{
	internal static string Plain(string name) => string.Concat(name.Where(Shown)).Trim();

	internal static bool Matches(string typed, string name)
	{
		var plain = Plain(name);
		return plain.Length > 0 && string.Equals(Plain(typed), plain, StringComparison.OrdinalIgnoreCase);
	}

	private static bool Shown(char character) => CharUnicodeInfo.GetUnicodeCategory(character) is not UnicodeCategory.Format;
}
