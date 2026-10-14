using System.Globalization;
using System.Text.RegularExpressions;
using API.Feeds;
using Domain.Media;

namespace API.Trakt;

internal static partial class TraktPages
{
	private const string Types = "movie,show,season,episode";

	private static readonly HashSet<string> Charts = ["trending", "popular", "anticipated"];

	internal static Optional<SiteLink> Parse(string input)
	{
		var trimmed = input.Trim();
		return UserPattern.IsMatch(trimmed)
			? Some.Of(new SiteLink($"users/{trimmed}/watchlist", []))
			: SiteLink.Parse(trimmed, "trakt.tv").Or(() => SiteLink.Parse(trimmed, "app.trakt.tv"));
	}

	internal static Result<SiteLink> Of(SiteLink link)
	{
		var segments = link.Path.Split('/');
		return segments switch
		{
			["users", var user] => Page($"users/{user}/watchlist"),
			["users", var user, "watchlist"] => Page($"users/{user}/watchlist"),
			["users", var user, "lists", var list] => Page($"users/{user}/lists/{list}"),
			["lists", var id] when id.All(char.IsAsciiDigit) => Page($"lists/{id}"),
			["movies" or "shows", var chart] when Charts.Contains(chart) => Page(link.Path),
			["discover", var chart] when Charts.Contains(chart) => Discovered(link, chart),
			_ => Error.From($"Sammlarr cannot follow Trakt's {link.Path} page. Paste a watchlist, a public list, or a trending, popular or anticipated chart."),
		};
	}

	internal static string[] Reads(string identifier)
	{
		return identifier.Split('/') switch
		{
			["discover", var chart] => [$"movies/{chart}", $"shows/{chart}"],
			_ => [identifier],
		};
	}

	internal static bool IsChart(string identifier)
	{
		return identifier.StartsWith("movies/", StringComparison.Ordinal) || identifier.StartsWith("shows/", StringComparison.Ordinal);
	}

	internal static Optional<MediaType> ChartType(string identifier)
	{
		return Optional.When(IsChart(identifier), () => identifier.StartsWith("shows/", StringComparison.Ordinal) ? MediaType.Show : MediaType.Movie);
	}

	internal static string Request(string identifier, int page)
	{
		var number = page.ToString(CultureInfo.InvariantCulture);
		var path = identifier switch
		{
			_ when identifier.EndsWith("/watchlist", StringComparison.Ordinal) => identifier,
			_ when IsChart(identifier) => identifier,
			_ => $"{identifier}/items/{Types}",
		};
		return $"{path}?page={number}&limit=250";
	}

	private static Result<SiteLink> Page(string path) => Success.Of(new SiteLink(path, []));

	private static Result<SiteLink> Discovered(SiteLink link, string chart)
	{
		return link.Values("mode").LastOrDefault() switch
		{
			"movie" => Page($"movies/{chart}"),
			"show" => Page($"shows/{chart}"),
			_ => Page($"discover/{chart}"),
		};
	}

	[GeneratedRegex(@"^[A-Za-z0-9_-]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex UserPattern { get; }
}
