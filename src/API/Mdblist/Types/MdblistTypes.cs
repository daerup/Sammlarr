using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Media;

namespace API.Mdblist.Types;

internal static class MdblistTypes
{
	public static class Lists
	{
		public sealed record Info([property: JsonPropertyName("user_name")] string UserName, [property: JsonPropertyName("slug")] string Slug);
	}

	public static class Titles
	{
		public sealed record Title(
			[property: JsonPropertyName("ids")] ListItems.Ids Ids,
			[property: JsonPropertyName("ratings")] IReadOnlyList<Rating>? Ratings,
			[property: JsonPropertyName("title")] string? Name = null,
			[property: JsonPropertyName("year")] int? Year = null
		);

		public sealed record Rating(
			[property: JsonPropertyName("source")] string Source,
			[property: JsonPropertyName("score")] float? Score,
			[property: JsonPropertyName("votes")] int? Votes,
			[property: JsonPropertyName("url")] JsonElement Url
		);
	}

	public static class ListItems
	{
		[JsonConverter(typeof(RootConverter))]
		public sealed record Root(IReadOnlyList<Movie> movies, IReadOnlyList<Show> shows, IReadOnlyList<UnmatchedListItem> unmatched);

		public sealed record UnmatchedListItem(MediaType MediaType, Ids ids, string title, int release_year);

		public interface IListItem
		{
			Ids ids { get; }
			string title { get; }
			int release_year { get; }
		}

		public sealed record Ids(
			[property: JsonPropertyName("imdb")] string? imdb,
			[property: JsonPropertyName("tmdb")] int? tmdb,
			[property: JsonPropertyName("tvdb")] int? tvdb,
			[property: JsonPropertyName("mdblist")] string? mdblist,
			[property: JsonPropertyName("mal")] JsonElement? mal,
			[property: JsonPropertyName("trakt")] int? trakt = null
		);

		public sealed record Movie(
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

		public sealed record Show(
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
