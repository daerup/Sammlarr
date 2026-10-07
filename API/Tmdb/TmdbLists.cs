using System.Text.RegularExpressions;

namespace API.Tmdb;

internal static partial class TmdbLists
{
	internal static Optional<string> Parse(string input)
	{
		var match = ListPattern().Match(input.Trim());
		return Optional.When(match.Success, () => match.Groups["id"].Value);
	}

	[GeneratedRegex(@"^(?:(?:https?://)?(?:www\.)?themoviedb\.org/list/)?(?<id>[0-9]+)(?:-[^/?#]*)?/?(?:[?#].*)?$", RegexOptions.IgnoreCase)]
	private static partial Regex ListPattern();
}
