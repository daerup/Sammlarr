using System.Text.Json.Serialization;

namespace API.AniList.Types;

internal static class AniListTypes
{
	public sealed record Answer<TData>([property: JsonPropertyName("data")] TData? Data, [property: JsonPropertyName("errors")] IReadOnlyList<Problem>? Errors);

	public sealed record Problem([property: JsonPropertyName("message")] string? Message, [property: JsonPropertyName("status")] int? Status);

	public sealed record MediaData([property: JsonPropertyName("Media")] Media? Media);

	public sealed record ListData([property: JsonPropertyName("MediaListCollection")] Collection? Collection);

	public sealed record Collection([property: JsonPropertyName("lists")] IReadOnlyList<List>? Lists);

	public sealed record List([property: JsonPropertyName("entries")] IReadOnlyList<ListItem>? Entries);

	public sealed record ListItem([property: JsonPropertyName("media")] Media? Media);

	public sealed record Media(
		[property: JsonPropertyName("id")] int Id,
		[property: JsonPropertyName("idMal")] int? IdMal,
		[property: JsonPropertyName("format")] string? Format,
		[property: JsonPropertyName("title")] Title? Title,
		[property: JsonPropertyName("description")] string? Description,
		[property: JsonPropertyName("coverImage")] Cover? CoverImage,
		[property: JsonPropertyName("startDate")] Date? StartDate,
		[property: JsonPropertyName("genres")] IReadOnlyList<string>? Genres,
		[property: JsonPropertyName("episodes")] int? Episodes,
		[property: JsonPropertyName("duration")] int? Duration,
		[property: JsonPropertyName("averageScore")] int? AverageScore,
		[property: JsonPropertyName("stats")] Stats? Stats
	);

	public sealed record Title([property: JsonPropertyName("english")] string? English, [property: JsonPropertyName("romaji")] string? Romaji);

	public sealed record Cover([property: JsonPropertyName("extraLarge")] string? ExtraLarge);

	public sealed record Date(
		[property: JsonPropertyName("year")] int? Year,
		[property: JsonPropertyName("month")] int? Month,
		[property: JsonPropertyName("day")] int? Day
	);

	public sealed record Stats([property: JsonPropertyName("scoreDistribution")] IReadOnlyList<Score>? ScoreDistribution);

	public sealed record Score([property: JsonPropertyName("score")] int ScoreValue, [property: JsonPropertyName("amount")] int Amount);
}
