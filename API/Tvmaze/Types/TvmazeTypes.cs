using System.Text.Json.Serialization;

namespace API.Tvmaze.Types;

internal static class TvmazeTypes
{
	public sealed record Show(
		[property: JsonPropertyName("id")] int Id,
		[property: JsonPropertyName("name")] string? Name,
		[property: JsonPropertyName("premiered")] string? Premiered,
		[property: JsonPropertyName("runtime")] int? Runtime,
		[property: JsonPropertyName("averageRuntime")] int? AverageRuntime,
		[property: JsonPropertyName("genres")] IReadOnlyList<string>? Genres,
		[property: JsonPropertyName("summary")] string? Summary,
		[property: JsonPropertyName("image")] Image? Image,
		[property: JsonPropertyName("externals")] Externals? Externals
	);

	public sealed record Image([property: JsonPropertyName("original")] string? Original);

	public sealed record Externals([property: JsonPropertyName("thetvdb")] int? Thetvdb, [property: JsonPropertyName("imdb")] string? Imdb);
}
