using System.Text.Json.Serialization;

namespace API.Trakt.Types;

internal static class TraktTypes
{
	public sealed record Ids(
		[property: JsonPropertyName("trakt")] int? Trakt,
		[property: JsonPropertyName("imdb")] string? Imdb,
		[property: JsonPropertyName("tmdb")] int? Tmdb,
		[property: JsonPropertyName("tvdb")] int? Tvdb
	);

	public sealed record Title(
		[property: JsonPropertyName("title")] string? Name,
		[property: JsonPropertyName("year")] int? Year,
		[property: JsonPropertyName("ids")] Ids? Ids,
		[property: JsonPropertyName("overview")] string? Overview,
		[property: JsonPropertyName("released")] string? Released,
		[property: JsonPropertyName("first_aired")] string? FirstAired,
		[property: JsonPropertyName("runtime")] int? Runtime,
		[property: JsonPropertyName("genres")] IReadOnlyList<string>? Genres,
		[property: JsonPropertyName("rating")] double? Rating,
		[property: JsonPropertyName("votes")] int? Votes
	);

	public sealed record Item(
		[property: JsonPropertyName("movie")] Title? Movie,
		[property: JsonPropertyName("show")] Title? Show,
		[property: JsonPropertyName("title")] string? Name,
		[property: JsonPropertyName("year")] int? Year,
		[property: JsonPropertyName("ids")] Ids? Ids
	);
}
