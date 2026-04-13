using System.Text.Json.Serialization;

namespace API.Mdblist.Types;

internal static class MdblistTypes
{
	public static class ListItems
	{
		public record Root([property: JsonPropertyName("movies")] IReadOnlyList<Movie> movies, [property: JsonPropertyName("shows")] IReadOnlyList<Show> shows);

		public interface IListItem
		{
			Ids ids { get; }
			string title { get; }
			int release_year { get; }
		}

		public record Ids(
			[property: JsonPropertyName("imdb")] string imdb,
			[property: JsonPropertyName("tmdb")] int tmdb,
			[property: JsonPropertyName("tvdb")] int tvdb,
			[property: JsonPropertyName("mdblist")] string mdblist,
			[property: JsonPropertyName("mal")] string mal
		);

		public record Movie(
			[property: JsonPropertyName("id")] int id,
			[property: JsonPropertyName("ids")] Ids ids,
			[property: JsonPropertyName("rank")] int rank,
			[property: JsonPropertyName("title")] string title,
			[property: JsonPropertyName("status")] string status,
			[property: JsonPropertyName("country")] string country,
			[property: JsonPropertyName("runtime")] int runtime,
			[property: JsonPropertyName("mediatype")] string mediatype,
			[property: JsonPropertyName("release_date")] string release_date,
			[property: JsonPropertyName("release_year")] int release_year,
			[property: JsonPropertyName("spoken_language")] string spoken_language
		) : IListItem;

		public record Show(
			[property: JsonPropertyName("id")] int id,
			[property: JsonPropertyName("ids")] Ids ids,
			[property: JsonPropertyName("rank")] int rank,
			[property: JsonPropertyName("title")] string title,
			[property: JsonPropertyName("status")] string status,
			[property: JsonPropertyName("country")] string country,
			[property: JsonPropertyName("runtime")] int runtime,
			[property: JsonPropertyName("mediatype")] string mediatype,
			[property: JsonPropertyName("release_date")] string release_date,
			[property: JsonPropertyName("release_year")] int release_year,
			[property: JsonPropertyName("spoken_language")] string spoken_language
		) : IListItem;
	}
}
