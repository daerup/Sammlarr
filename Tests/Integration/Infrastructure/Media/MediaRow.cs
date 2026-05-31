using Contracts.Media;

namespace Integration.Tests.Infrastructure.Media;

public sealed class MediaRow
{
	public string? Media { get; set; }
	public string? Type { get; set; }
	public IReadOnlyDictionary<MediaProvider, string>? Ids { get; set; }
	public string? Title { get; set; }
	public string? Summary { get; set; }
	public string? Poster { get; set; }
	public int? Year { get; set; }
	public IReadOnlyList<string>? Genres { get; set; }
	public string? Fetched { get; set; }
	public string? VoteAverage { get; set; }
	public int? VoteCount { get; set; }
	public string? Origin { get; set; }
	public string? Rating { get; set; }
}

public sealed record ResolvedVersion(
	IReadOnlyDictionary<MediaProvider, string> Ids,
	MediaType Type,
	int Year,
	string Title,
	string? Poster,
	string? Summary,
	IReadOnlyList<string>? Genres
);
