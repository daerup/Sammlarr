using System.Text.Json.Serialization;

namespace API.Mal.Types;

internal static class MalTypes
{
	public sealed record ListPage([property: JsonPropertyName("data")] IReadOnlyList<ListItem>? Data, [property: JsonPropertyName("paging")] Paging? Paging);

	public sealed record ListItem([property: JsonPropertyName("node")] Anime? Node);

	public sealed record Paging([property: JsonPropertyName("next")] string? Next);

	public sealed record Anime(
		[property: JsonPropertyName("id")] int Id,
		[property: JsonPropertyName("title")] string? Title,
		[property: JsonPropertyName("alternative_titles")] AlternativeTitles? AlternativeTitles,
		[property: JsonPropertyName("media_type")] string? MediaType,
		[property: JsonPropertyName("start_date")] string? StartDate,
		[property: JsonPropertyName("synopsis")] string? Synopsis,
		[property: JsonPropertyName("main_picture")] Picture? MainPicture,
		[property: JsonPropertyName("genres")] IReadOnlyList<Genre>? Genres,
		[property: JsonPropertyName("num_episodes")] int? NumEpisodes,
		[property: JsonPropertyName("average_episode_duration")] int? AverageEpisodeDuration,
		[property: JsonPropertyName("mean")] float? Mean,
		[property: JsonPropertyName("num_scoring_users")] int? NumScoringUsers
	);

	public sealed record AlternativeTitles([property: JsonPropertyName("en")] string? En);

	public sealed record Picture([property: JsonPropertyName("large")] string? Large, [property: JsonPropertyName("medium")] string? Medium);

	public sealed record Genre([property: JsonPropertyName("name")] string? Name);
}
