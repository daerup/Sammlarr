using System.Globalization;
using System.Text.RegularExpressions;
using API.Feeds;
using Domain.Media;

namespace API.Tmdb;

internal static partial class TmdbPages
{
	private const string Site = "TMDB";

	private static readonly Dictionary<string, string> Categories = new(StringComparer.Ordinal)
	{
		["movie"] = "movie/popular",
		["movie/now-playing"] = "movie/now_playing",
		["movie/upcoming"] = "movie/upcoming",
		["movie/top-rated"] = "movie/top_rated",
		["tv"] = "tv/popular",
		["tv/airing-today"] = "tv/airing_today",
		["tv/on-the-air"] = "tv/on_the_air",
		["tv/top-rated"] = "tv/top_rated",
	};

	private static readonly HashSet<string> Dropped = ["view"];

	private static readonly HashSet<string> MovieCategoryKeys = ["region", "language"];

	private static readonly HashSet<string> TvCategoryKeys = ["language", "timezone"];

	private static readonly HashSet<string> Shared =
	[
		"include_adult",
		"language",
		"sort_by",
		"vote_average.gte",
		"vote_average.lte",
		"vote_count.gte",
		"vote_count.lte",
		"watch_region",
		"with_companies",
		"with_genres",
		"with_keywords",
		"with_origin_country",
		"with_original_language",
		"with_runtime.gte",
		"with_runtime.lte",
		"with_watch_monetization_types",
		"with_watch_providers",
		"without_companies",
		"without_genres",
		"without_keywords",
		"without_watch_providers",
	];

	private static readonly HashSet<string> MovieKeys =
	[
		.. Shared,
		"certification",
		"certification.gte",
		"certification.lte",
		"certification_country",
		"include_video",
		"primary_release_year",
		"primary_release_date.gte",
		"primary_release_date.lte",
		"region",
		"release_date.gte",
		"release_date.lte",
		"with_cast",
		"with_crew",
		"with_people",
		"with_release_type",
		"year",
	];

	private static readonly HashSet<string> TvKeys =
	[
		.. Shared,
		"air_date.gte",
		"air_date.lte",
		"first_air_date_year",
		"first_air_date.gte",
		"first_air_date.lte",
		"include_null_first_air_dates",
		"screened_theatrically",
		"timezone",
		"with_networks",
		"with_status",
		"with_type",
	];

	internal static bool IsPage(string identifier) => !identifier.All(char.IsAsciiDigit);

	internal static Result<SiteLink> Of(SiteLink link)
	{
		var page = Canonical(link);
		var kept = page.Knowing(KeysOf(page.Path), Dropped, Site);
		return Refused(page).Match<string, Result<SiteLink>>(reason => Error.From(reason), () => kept);
	}

	private static MediaType TypeOf(SiteLink page)
	{
		return page.Path.StartsWith("tv", StringComparison.Ordinal) || page.Path.EndsWith("/tv", StringComparison.Ordinal) ? MediaType.Show : MediaType.Movie;
	}

	internal static string Kind(SiteLink page) => TypeOf(page) is MediaType.Show ? "tv" : "movie";

	internal static string Request(SiteLink page, int number)
	{
		var (endpoint, keys) = Endpoint(page);
		return new SiteLink(endpoint, [.. keys, KeyValuePair.Create("page", number.ToString(CultureInfo.InvariantCulture))]).Stored();
	}

	private static (string Endpoint, IEnumerable<KeyValuePair<string, string>> Keys) Endpoint(SiteLink page)
	{
		var by = ByPattern().Match(page.Path);
		if (by.Success)
		{
			return ($"discover/{Kind(page)}", [KeyValuePair.Create(DiscoverKey(by.Groups["kind"].Value), by.Groups["id"].Value), .. page.Query]);
		}

		var category = Categories.GetValueOrNone(page.Path).When(_ => !Discovers(page));
		return (category.GetValueOr(() => Discovers(page) ? $"discover/{Kind(page)}" : page.Path), page.Query);
	}

	private static bool Discovers(SiteLink page) => page.Path is "movie" or "tv" && page.Query.Any(pair => !CategoryKeysOf(page.Path).Contains(pair.Key));

	private static IReadOnlySet<string> CategoryKeysOf(string path) => path.StartsWith("tv", StringComparison.Ordinal) ? TvCategoryKeys : MovieCategoryKeys;

	private static SiteLink Canonical(SiteLink link)
	{
		var by = SluggedPattern().Match(link.Path);
		return by.Success ? link with { Path = $"{by.Groups["kind"].Value}/{by.Groups["id"].Value}/{by.Groups["type"].Value}" } : link;
	}

	private static IReadOnlySet<string> KeysOf(string path)
	{
		return path switch
		{
			"discover/movie" => MovieKeys,
			"discover/tv" => TvKeys,
			"movie" => MovieKeys,
			"tv" => TvKeys,
			_ when Categories.ContainsKey(path) => CategoryKeysOf(path),
			_ when ByPattern().IsMatch(path) => path.EndsWith("/tv", StringComparison.Ordinal) ? TvKeys : MovieKeys,
			_ => new HashSet<string>(),
		};
	}

	private static Optional<string> Refused(SiteLink page)
	{
		var known = page.Path is "discover/movie" or "discover/tv" || Categories.ContainsKey(page.Path) || ByPattern().IsMatch(page.Path);
		return page.Values("discover_preset")
			.FirstOrNone()
			.Map(preset => $"Sammlarr cannot follow TMDB's {preset} preset, since the API has no list for it. Paste the discover link its filters give.")
			.Or(() => Optional.When(!known, () => $"Sammlarr cannot follow TMDB's {page.Path} page. Paste a list, a movie or TV page, or a discover link."))
			.Or(() =>
				Optional.When(
					page.Values("with_watch_providers").Count > 0 && page.Values("watch_region").Count == 0,
					() => "TMDB counts streaming services within one country only, so a link with with_watch_providers needs its watch_region."
				)
			)
			.Or(() =>
				Optional.When(
					page.Values("with_watch_monetization_types").Count > 0 && page.Values("watch_region").Count == 0,
					() => "TMDB counts how a title streams within one country only, so a link with with_watch_monetization_types needs its watch_region."
				)
			)
			.Or(() =>
				Optional.When(
					page.Query.Any(pair => pair.Key.StartsWith("certification", StringComparison.Ordinal) && pair.Key != "certification_country")
						&& page.Values("certification_country").Count == 0,
					() => "TMDB rates certifications per country, so a link with a certification needs its certification_country."
				)
			);
	}

	private static string DiscoverKey(string kind)
	{
		return kind switch
		{
			"keyword" => "with_keywords",
			"company" => "with_companies",
			_ => "with_networks",
		};
	}

	[GeneratedRegex(@"^(?<kind>keyword|company)/(?<id>[0-9]+)/(?:movie|tv)$|^(?<kind>network)/(?<id>[0-9]+)/tv$")]
	private static partial Regex ByPattern();

	[GeneratedRegex(@"^(?<kind>keyword|company|network)/(?<id>[0-9]+)(?:-[^/]*)?/(?<type>movie|tv)$")]
	private static partial Regex SluggedPattern();
}
