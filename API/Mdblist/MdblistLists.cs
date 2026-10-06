using System.Globalization;
using System.Text.RegularExpressions;

namespace API.Mdblist;

internal static partial class MdblistLists
{
	internal static Optional<string> Parse(string input)
	{
		var match = Address().Match(input.Trim());
		return Optional.When(match.Success, () => match.Groups["list"].Value);
	}

	internal static string ItemsPath(string list)
	{
		var external = External().Match(list);
		return external.Success
			? string.Format(CultureInfo.InvariantCulture, MdblistEndpoints.ExternalListItems, external.Groups["id"].Value)
			: string.Format(CultureInfo.InvariantCulture, MdblistEndpoints.ListItems, list);
	}

	[GeneratedRegex(
		@"^(?:(?:https?://)?(?:www\.)?mdblist\.com/lists/)?(?<list>\d+|[\w-][\w.-]*/external/\d+|[\w-][\w.-]*/[\w-][\w.-]*)/?(?:[?#].*)?$",
		RegexOptions.IgnoreCase
	)]
	private static partial Regex Address();

	[GeneratedRegex(@"^[\w-][\w.-]*/external/(?<id>\d+)$", RegexOptions.IgnoreCase)]
	private static partial Regex External();
}
