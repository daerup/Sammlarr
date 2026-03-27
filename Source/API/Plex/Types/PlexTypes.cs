using System.Text.Json.Serialization;
using Contracts.Media;

namespace API.Plex.Types;

public static class PlexTypes
{
	public static class Watchlist
	{
		public record MediaContainer(
			[property: JsonPropertyName("identifier")] string Identifier,
			[property: JsonPropertyName("offset")] int Offset,
			[property: JsonPropertyName("totalSize")] int TotalSize,
			[property: JsonPropertyName("size")] int Size,
			[property: JsonPropertyName("Metadata")] IReadOnlyList<Metadata> Metadata
		);

		public record Metadata(
			[property: JsonPropertyName("art")] string Art,
			[property: JsonPropertyName("rating")] double Rating,
			[property: JsonPropertyName("ratingKey")] string RatingKey,
			[property: JsonPropertyName("tagline")] string? Tagline,
			[property: JsonPropertyName("type")] string Type,
			[property: JsonPropertyName("addedAt")] int AddedAt,
			[property: JsonPropertyName("isContinuingSeries")] bool IsContinuingSeries,
			[property: JsonPropertyName("originallyAvailableAt")] string OriginallyAvailableAt,
			[property: JsonPropertyName("thumb")] string Thumb,
			[property: JsonPropertyName("title")] string Title,
			[property: JsonPropertyName("year")] int Year
		)
		{
			public PlexId PlexId => PlexId.From(RatingKey);
		}

		public record Root([property: JsonPropertyName("MediaContainer")] MediaContainer MediaContainer);
	}

	public static class ById
	{
		public record Guid([property: JsonPropertyName("id")] string Id);

		public record MediaContainer(
			[property: JsonPropertyName("librarySectionID")] string LibrarySectionId,
			[property: JsonPropertyName("librarySectionTitle")] string LibrarySectionTitle,
			[property: JsonPropertyName("offset")] int Offset,
			[property: JsonPropertyName("totalSize")] int TotalSize,
			[property: JsonPropertyName("Metadata")] IReadOnlyList<Metadata> Metadata
		);

		public record Metadata(
			[property: JsonPropertyName("art")] string Art,
			[property: JsonPropertyName("ratingKey")] string RatingKey,
			[property: JsonPropertyName("summary")] string Summary,
			[property: JsonPropertyName("tagline")] string Tagline,
			[property: JsonPropertyName("type")] string Type,
			[property: JsonPropertyName("addedAt")] int AddedAt,
			[property: JsonPropertyName("audienceRating")] double AudienceRating,
			[property: JsonPropertyName("originallyAvailableAt")] string OriginallyAvailableAt,
			[property: JsonPropertyName("thumb")] string Poster,
			[property: JsonPropertyName("title")] string Title,
			[property: JsonPropertyName("year")] int Year,
			[property: JsonPropertyName("Guid")] IReadOnlyList<Guid> Guid
		)
		{
			public PlexId PlexId => PlexId.From(RatingKey);
			public TmdbId[] TmdbId => GetId("tmdb://", Contracts.Media.TmdbId.Parse);
			public ImdbId[] ImdbId => GetId("imdb://", Contracts.Media.ImdbId.From);
			public TvdbId[] TvdbId => GetId("tvdb://", Contracts.Media.TvdbId.Parse);

			private T[] GetId<T>(string prefix, Func<string, T> convert) =>
				Guid.Where(g => g.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(g => convert(g.Id[prefix.Length..])).ToArray();
		};

		public record Root([property: JsonPropertyName("MediaContainer")] MediaContainer MediaContainer);
	}

	public static class Search
	{
		public record MediaContainer(
			[property: JsonPropertyName("suggestedTerms")] IReadOnlyList<object> SuggestedTerms,
			[property: JsonPropertyName("identifier")] string Identifier,
			[property: JsonPropertyName("size")] int Size,
			[property: JsonPropertyName("SearchResults")] IReadOnlyList<SearchResult> SearchResults
		);

		public record Metadata(
			[property: JsonPropertyName("ratingKey")] string RatingKey,
			[property: JsonPropertyName("title")] string Title,
			[property: JsonPropertyName("thumb")] string? Thumb,
			[property: JsonPropertyName("type")] string Type,
			[property: JsonPropertyName("year")] int Year,
			[property: JsonPropertyName("summary")] string? Summary,
			[property: JsonPropertyName("tagline")] string? Tagline,
			[property: JsonPropertyName("rating")] double? Rating,
			[property: JsonPropertyName("audienceRating")] double? AudienceRating,
			[property: JsonPropertyName("duration")] int? Duration,
			[property: JsonPropertyName("Genre")] IReadOnlyList<Genre>? Genre,
			[property: JsonPropertyName("originallyAvailableAt")] string? OriginallyAvailableAt
		)
		{
			public PlexId PlexId => PlexId.From(RatingKey);
		};

		public record Genre([property: JsonPropertyName("tag")] string Tag);

		public record Root([property: JsonPropertyName("MediaContainer")] MediaContainer MediaContainer);

		public record SearchResult(
			[property: JsonPropertyName("size")] int Size,
			[property: JsonPropertyName("SearchResult")] IReadOnlyList<NestedSearchResult> NestedSearchResult
		);

		public record NestedSearchResult([property: JsonPropertyName("Metadata")] Metadata Metadata);
	}

	public static class Auth
	{
		public record Root(
			[property: JsonPropertyName("id")] int PinId,
			[property: JsonPropertyName("code")] string Code,
			[property: JsonPropertyName("product")] string Product,
			[property: JsonPropertyName("trusted")] bool Trusted,
			[property: JsonPropertyName("qr")] string Qr,
			[property: JsonPropertyName("clientIdentifier")] string ClientIdentifier,
			[property: JsonPropertyName("expiresAt")] DateTime ExpiresAt,
			[property: JsonPropertyName("authToken")] object AuthToken,
			[property: JsonPropertyName("newRegistration")] object NewRegistration
		);
	}

	public static class User
	{
		public record Root(
			[property: JsonPropertyName("username")] string Username,
			[property: JsonPropertyName("title")] string Title,
			[property: JsonPropertyName("thumb")] string? Thumb
		);
	}
}
