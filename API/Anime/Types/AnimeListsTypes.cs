using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Anime.Types;

internal static class AnimeListsTypes
{
	public sealed record Anime(
		[property: JsonPropertyName("type")] string? Type,
		[property: JsonPropertyName("anilist_id")] int? AniList,
		[property: JsonPropertyName("mal_id")] int? Mal,
		[property: JsonPropertyName("tvdb_id")] int? Tvdb,
		[property: JsonPropertyName("themoviedb_id")] Tmdb? Tmdb,
		[property: JsonPropertyName("imdb_id")] IReadOnlyList<string>? Imdb,
		[property: JsonPropertyName("season")] PerSite? Season,
		[property: JsonPropertyName("episode_offset")] PerSite? EpisodeOffset
	);

	public sealed record Tmdb([property: JsonPropertyName("tv")] int? Tv, [property: JsonPropertyName("movie")] JsonElement? Movie);

	public sealed record PerSite([property: JsonPropertyName("tvdb")] int? Tvdb, [property: JsonPropertyName("tmdb")] int? Tmdb);
}
