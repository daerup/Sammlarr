using System.Text.Json;

namespace E2E.Tests;

internal static class WireMockStubs
{
	private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wiremock"));

	private static readonly PolicyFile Policies = LoadPolicies();

	internal static string MappingsDirectory => Path.Combine(Root, "mappings");

	internal static string ResponsesDirectory => Path.Combine(Root, "responses");

	internal static FixturePolicy PolicyFor(string path) =>
		Matches(Policies.Frozen, path) ? FixturePolicy.Frozen
		: Matches(Policies.Contract, path) ? FixturePolicy.Contract
		: FixturePolicy.Value;

	private static bool Matches(IReadOnlyList<string> patterns, string path) =>
		patterns.Any(pattern =>
			pattern.EndsWith('*') ? path.StartsWith(pattern[..^1], StringComparison.Ordinal) : string.Equals(path, pattern, StringComparison.Ordinal)
		);

	private static PolicyFile LoadPolicies()
	{
		var file = Path.Combine(Root, "fixture-policy.json");
		return File.Exists(file)
			? JsonSerializer.Deserialize<PolicyFile>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PolicyFile()
			: new PolicyFile();
	}

	private sealed record PolicyFile
	{
		public List<string> Contract { get; init; } = [];

		public List<string> Frozen { get; init; } = [];
	}

	internal static string ExpectedTrendingTitle()
	{
		var first = FirstTrendingResult();
		return first.TryGetProperty("title", out var title) && title.ValueKind is JsonValueKind.String
			? title.GetString()!
			: first.GetProperty("name").GetString()!;
	}

	internal static string ExpectedTrendingTmdbId()
	{
		var id = FirstTrendingResult().GetProperty("id");
		return id.ValueKind is JsonValueKind.String ? id.GetString()! : id.GetRawText();
	}

	internal static string ExpectedTrendingPlexId()
	{
		var title = ExpectedTrendingTitle();
		var tmdbId = ExpectedTrendingTmdbId();
		foreach (var file in Directory.EnumerateFiles(Path.Combine(ResponsesDirectory, "plex-discover"), "library-search-*.verified.json"))
		{
			using var document = JsonDocument.Parse(File.ReadAllText(file));
			foreach (var key in RatingKeys(document.RootElement, title))
			{
				if (DescribesTmdbId(key, tmdbId))
				{
					return key;
				}
			}
		}

		throw new InvalidOperationException($"No recorded Plex metadata fixture backs the trending title '{title}' as tmdb://{tmdbId}.");
	}

	private static bool DescribesTmdbId(string ratingKey, string tmdbId)
	{
		var fixture = Path.Combine(ResponsesDirectory, "plex-metadata", $"library-metadata-{ratingKey}.verified.json");
		return File.Exists(Path.Combine(MappingsDirectory, "plex-metadata", $"library-metadata-{ratingKey}.json"))
			&& File.Exists(fixture)
			&& File.ReadAllText(fixture).Contains($"tmdb://{tmdbId}", StringComparison.Ordinal);
	}

	private static IEnumerable<string> RatingKeys(JsonElement root, string title) =>
		root.GetProperty("MediaContainer")
			.GetProperty("SearchResults")
			.EnumerateArray()
			.SelectMany(group => group.TryGetProperty("SearchResult", out var results) ? results.EnumerateArray() : [])
			.Select(result => result.GetProperty("Metadata"))
			.Where(metadata => metadata.TryGetProperty("title", out var found) && found.GetString() == title)
			.Select(metadata => metadata.GetProperty("ratingKey").GetString()!);

	private static JsonElement FirstTrendingResult()
	{
		using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(ResponsesDirectory, "tmdb", "trending-all-week.verified.json")));
		return document.RootElement.GetProperty("results")[0].Clone();
	}
}
