using System.Globalization;
using API.Feeds;

namespace API.Mal;

internal static class MalPages
{
	private const string Fields = "media_type,start_date,alternative_titles";

	private static readonly IReadOnlySet<string> RankingTypes = new HashSet<string>(
		["all", "airing", "upcoming", "tv", "ova", "movie", "special", "bypopularity", "favorite"],
		StringComparer.Ordinal
	);

	private const string Top = "topanime.php";

	private const string Season = "anime/season";

	private static readonly string[] Seasons = ["winter", "spring", "summer", "fall"];

	private static readonly HashSet<string> TopKeys = ["type", "limit"];

	internal static bool IsPage(string identifier) =>
		identifier.StartsWith(Top, StringComparison.Ordinal) || identifier.StartsWith(Season, StringComparison.Ordinal);

	internal static Result<SiteLink> Of(SiteLink link)
	{
		var segments = link.Path.Split('/');
		return link.Path switch
		{
			Top => link.Knowing(TopKeys, "MyAnimeList").Map(Ranked),
			Season or $"{Season}/schedule" => link.Knowing(new HashSet<string>(), "MyAnimeList"),
			_ when segments.Length == 4 && $"{segments[0]}/{segments[1]}" == Season && IsYear(segments[2]) && Seasons.Contains(segments[3]) => link.Knowing(
				new HashSet<string>(),
				"MyAnimeList"
			),
			"anime.php" => Error.From("Sammlarr cannot follow MyAnimeList's search, since it ranks by relevance. Paste a top anime or season link."),
			_ => Error.From($"Sammlarr cannot follow MyAnimeList's {link.Path} page. Paste a list, a top anime or a season link."),
		};
	}

	internal static string Request(SiteLink page, DateOnly today)
	{
		var segments = page.Path.Split('/');
		var season = segments.Length == 4 ? $"{segments[2]}/{segments[3]}" : Current(today);
		return page.Path == Top
			? $"anime/ranking?ranking_type={page.Values("type").LastOrDefault("all")}&limit=500&offset={page.Values("limit").LastOrDefault("0")}&fields={Fields}"
			: $"anime/season/{season}?sort=anime_num_list_users&limit=500&fields={Fields}";
	}

	private static Result<SiteLink> Ranked(SiteLink top)
	{
		return top.Values("type")
			.FirstOrNone(type => !RankingTypes.Contains(type))
			.Map(type => $"MyAnimeList's API ranks no {type} top anime, so Sammlarr cannot follow it.")
			.Or(() =>
				top.Values("limit")
					.FirstOrNone(limit => !int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out _))
					.Map(limit => $"MyAnimeList's limit {limit} is not a number.")
			)
			.Match<string, Result<SiteLink>>(reason => Error.From(reason), () => Success.Of(top));
	}

	private static string Current(DateOnly today)
	{
		var season = AnimeSeason.Of(today);
		return $"{season.Year.ToString(CultureInfo.InvariantCulture)}/{season.Name.ToLowerInvariant()}";
	}

	private static bool IsYear(string value) => value.Length == 4 && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
