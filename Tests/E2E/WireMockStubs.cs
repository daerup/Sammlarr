using System.Text.Json;

namespace E2E.Tests;

internal static class WireMockStubs
{
	private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wiremock"));

	private static readonly IReadOnlyList<string> VolatilePaths = LoadVolatilePaths();

	internal static string MappingsDirectory => Path.Combine(Root, "mappings");

	internal static string ResponsesDirectory => Path.Combine(Root, "responses");

	internal static bool IsVolatile(string path) =>
		VolatilePaths.Any(pattern =>
			pattern.EndsWith('*') ? path.StartsWith(pattern[..^1], StringComparison.Ordinal) : string.Equals(path, pattern, StringComparison.Ordinal)
		);

	private static IReadOnlyList<string> LoadVolatilePaths()
	{
		var file = Path.Combine(Root, "volatile.json");
		return File.Exists(file) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file)) ?? [] : [];
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

	private static JsonElement FirstTrendingResult()
	{
		using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(ResponsesDirectory, "tmdb", "trending-all-week.verified.json")));
		return document.RootElement.GetProperty("results")[0].Clone();
	}
}
