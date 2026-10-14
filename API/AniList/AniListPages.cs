using System.Globalization;
using System.Text.Json.Nodes;
using API.Feeds;

namespace API.AniList;

internal static class AniListPages
{
	internal const string Search = "search/anime";

	private static readonly HashSet<string> Labels =
	[
		"search",
		"genres",
		"excludeGenres",
		"minimumTagRank",
		"year",
		"season",
		"format",
		"airing status",
		"source material",
		"country of origin",
		"year range",
		"sort",
	];

	private static readonly HashSet<string> Numeric = ["minimumTagRank", "year", "year range"];

	private static readonly Dictionary<string, KeyValuePair<string, string>[]> Presets = new(StringComparer.Ordinal)
	{
		["trending"] = [Label("sort", "TRENDING_DESC")],
		["popular"] = [Label("sort", "POPULARITY_DESC")],
		["top-100"] = [Label("sort", "SCORE_DESC")],
		["new"] = [Label("sort", "ID_DESC")],
		["favorites"] = [Label("sort", "FAVOURITES_DESC")],
		["top-tv"] = [Label("format", "TV"), Label("sort", "SCORE_DESC")],
		["top-tv-shorts"] = [Label("format", "TV_SHORT"), Label("sort", "SCORE_DESC")],
		["top-movies"] = [Label("format", "MOVIE"), Label("sort", "SCORE_DESC")],
		["top-ova"] = [Label("format", "OVA"), Label("sort", "SCORE_DESC")],
		["top-ona"] = [Label("format", "ONA"), Label("sort", "SCORE_DESC")],
		["this-season"] = [],
		["next-season"] = [],
	};

	internal static Result<SiteLink> Of(SiteLink link)
	{
		var known = link.Path == Search || Presets.ContainsKey(PresetOf(link));
		return known
			? link.Knowing(Labels, "AniList")
				.Map(found => Unreadable(found.Query).Match<string, Result<SiteLink>>(reason => Error.From(reason), () => Success.Of(found)))
			: Error.From($"Sammlarr cannot follow AniList's {link.Path} page. Paste a search/anime link or a list.");
	}

	internal static bool NeedsGenres(SiteLink search) => search.Values("genres").Count > 0 || search.Values("excludeGenres").Count > 0;

	internal static bool AsksForMusic(SiteLink search) => search.Values("format").Any(format => format.Equals("MUSIC", StringComparison.OrdinalIgnoreCase));

	internal static Result<JsonObject> Variables(SiteLink search, IReadOnlySet<string> genres, DateOnly today)
	{
		var labels = LabelsOf(search, today);
		return Unreadable(labels).Match<string, Result<JsonObject>>(reason => Error.From(reason), () => Success.Of(Build(labels, genres)));
	}

	private static Optional<string> Unreadable(IReadOnlyList<KeyValuePair<string, string>> labels)
	{
		var range = labels.Count(pair => pair.Key == "year range");
		return labels
			.Where(pair => Numeric.Contains(pair.Key))
			.FirstOrNone(pair => !int.TryParse(pair.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
			.Map(pair => $"AniList's {pair.Key} {pair.Value} is not a number Sammlarr can follow.")
			.Or(() => Optional.When(range is not (0 or 2), () => "AniList's year range needs a first and a last year."));
	}

	private static JsonObject Build(List<KeyValuePair<string, string>> labels, IReadOnlySet<string> genres)
	{
		var variables = new JsonObject { ["type"] = "ANIME" };
		var seasonal = labels.Any(pair => pair.Key == "season");
		var searched = labels.Any(pair => pair.Key == "search");
		foreach (var group in labels.GroupBy(pair => pair.Key))
		{
			var values = group.Select(pair => pair.Value).ToList();
			var last = values[^1];
			switch (group.Key)
			{
				case "search":
					variables["search"] = last;
					break;
				case "genres":
					Split(variables, values, genres, "genres", "tags");
					break;
				case "excludeGenres":
					Split(variables, values, genres, "excludedGenres", "excludedTags");
					break;
				case "minimumTagRank":
					variables["minimumTagRank"] = Int(last);
					break;
				case "year" when seasonal:
					variables["seasonYear"] = Int(last);
					break;
				case "year":
					variables["year"] = $"{Int(last)}%";
					break;
				case "season":
					variables["season"] = last.ToUpperInvariant();
					break;
				case "format":
					variables["format"] = Array(values.Select(value => value.ToUpperInvariant()));
					break;
				case "airing status":
					variables["status"] = last;
					break;
				case "source material":
					variables["source"] = last;
					break;
				case "country of origin":
					variables["countryOfOrigin"] = last;
					break;
				case "year range":
					variables["yearGreater"] = Int(values[0]) * 10000 - 1;
					variables["yearLesser"] = (Int(values[1]) + 1) * 10000;
					break;
				case "sort":
					variables["sort"] = Sort(last, searched);
					break;
			}
		}

		return variables;
	}

	private static List<KeyValuePair<string, string>> LabelsOf(SiteLink search, DateOnly today)
	{
		var preset = PresetOf(search);
		KeyValuePair<string, string>[] seasonal = preset switch
		{
			"this-season" => Season(AnimeSeason.Of(today)),
			"next-season" => Season(AnimeSeason.Of(today).Next()),
			_ => [],
		};
		return [.. Presets.GetValueOrNone(preset).GetValueOr([]), .. seasonal, .. search.Query];
	}

	private static string PresetOf(SiteLink link) =>
		link.Path.StartsWith($"{Search}/", StringComparison.Ordinal) ? link.Path[(Search.Length + 1)..] : string.Empty;

	private static KeyValuePair<string, string>[] Season(AnimeSeason season) =>
		[Label("season", season.Name), Label("year", season.Year.ToString(CultureInfo.InvariantCulture))];

	private static JsonArray Sort(string sort, bool searched)
	{
		return sort switch
		{
			"TRENDING_DESC" => Array(["TRENDING_DESC", "POPULARITY_DESC"]),
			"SEARCH_MATCH" when !searched => Array(["POPULARITY_DESC"]),
			_ => Array([sort]),
		};
	}

	private static void Split(JsonObject variables, List<string> values, IReadOnlySet<string> genres, string genreKey, string tagKey)
	{
		var named = values.SelectMany(value => value.Split(',')).ToList();
		Optional.When(named.Any(genres.Contains), () => Array(named.Where(genres.Contains))).Apply(found => variables[genreKey] = found);
		Optional
			.When(named.Any(name => !genres.Contains(name)), () => Array(named.Where(name => !genres.Contains(name))))
			.Apply(found => variables[tagKey] = found);
	}

	private static JsonArray Array(IEnumerable<string> values) => [.. values.Select(value => (JsonNode)value)];

	private static int Int(string value) => int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

	private static KeyValuePair<string, string> Label(string label, string value) => KeyValuePair.Create(label, value);
}
