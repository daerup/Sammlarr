using System.Text.Json.Serialization;
using Domain.Media;

namespace API.Plex.Types;

internal static class PlexTypes
{
	internal static class Watchlist
	{
		public sealed record MediaContainer(
			[property: JsonPropertyName("identifier")] string Identifier,
			[property: JsonPropertyName("offset")] int Offset,
			[property: JsonPropertyName("totalSize")] int TotalSize,
			[property: JsonPropertyName("size")] int Size,
			[property: JsonPropertyName("Metadata")] IReadOnlyList<Metadata>? Items
		);

		public sealed record Metadata(
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
			private MediaType MediaType => MediaType.Parse(Type);
			public PlexId PlexId => PlexId.From(RatingKey, MediaType);
		}

		public sealed record Root([property: JsonPropertyName("MediaContainer")] MediaContainer MediaContainer);
	}

	internal static class ById
	{
		public sealed record Guid([property: JsonPropertyName("id")] string Id);

		public sealed record MediaContainer(
			[property: JsonPropertyName("librarySectionID")] string LibrarySectionId,
			[property: JsonPropertyName("librarySectionTitle")] string LibrarySectionTitle,
			[property: JsonPropertyName("offset")] int Offset,
			[property: JsonPropertyName("totalSize")] int TotalSize,
			[property: JsonPropertyName("Metadata")] IReadOnlyList<Metadata> Items
		);

		public sealed record Metadata(
			[property: JsonPropertyName("art")] string Art,
			[property: JsonPropertyName("ratingKey")] string RatingKey,
			[property: JsonPropertyName("summary")] string? Summary,
			[property: JsonPropertyName("tagline")] string? Tagline,
			[property: JsonPropertyName("type")] string Type,
			[property: JsonPropertyName("addedAt")] long AddedAt,
			[property: JsonPropertyName("audienceRating")] double AudienceRating,
			[property: JsonPropertyName("originallyAvailableAt")] string OriginallyAvailableAt,
			[property: JsonPropertyName("thumb")] string Poster,
			[property: JsonPropertyName("title")] string? Title,
			[property: JsonPropertyName("year")] int Year,
			[property: JsonPropertyName("duration")] int Duration,
			[property: JsonPropertyName("childCount")] int? SeasonCount,
			[property: JsonPropertyName("leafCount")] int? EpisodeCount,
			[property: JsonPropertyName("Guid")] IReadOnlyList<Guid> Guids,
			[property: JsonPropertyName("Genre")] IReadOnlyList<Genre> Genres,
			[property: JsonPropertyName("Rating")] IReadOnlyList<Rating>? Ratings = null,
			[property: JsonPropertyName("imdbRatingCount")] int? ImdbRatingCount = null
		)
		{
			private MediaType MediaType => MediaType.Parse(Type);
			public PlexId PlexId => PlexId.From(RatingKey, MediaType);
			public TmdbId[] TmdbIds => GetId("tmdb://", i => TmdbId.From(i, MediaType));
			public ImdbId[] ImdbIds => GetId("imdb://", i => ImdbId.From(i, MediaType));
			public TvdbId[] TvdbIds => GetId("tvdb://", i => TvdbId.From(i, MediaType));

			private T[] GetId<T>(string prefix, Func<string, T> convert) =>
				Guids.Where(g => g.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(g => convert(g.Id[prefix.Length..])).ToArray();
		}

		public sealed record Genre([property: JsonPropertyName("tag")] string Tag);

		public sealed record Rating(
			[property: JsonPropertyName("image")] string Image,
			[property: JsonPropertyName("type")] string Type,
			[property: JsonPropertyName("value")] double Value
		);

		public sealed record Root([property: JsonPropertyName("MediaContainer")] MediaContainer MediaContainer);
	}

	internal static class Search
	{
		public sealed record MediaContainer(
			[property: JsonPropertyName("suggestedTerms")] IReadOnlyList<object> SuggestedTerms,
			[property: JsonPropertyName("identifier")] string Identifier,
			[property: JsonPropertyName("size")] int Size,
			[property: JsonPropertyName("SearchResults")] IReadOnlyList<SearchResult> SearchResults
		);

		public sealed record Metadata(
			[property: JsonPropertyName("ratingKey")] string RatingKey,
			[property: JsonPropertyName("title")] string Title,
			[property: JsonPropertyName("thumb")] string? Thumb,
			[property: JsonPropertyName("art")] string? Art,
			[property: JsonPropertyName("type")] string Type,
			[property: JsonPropertyName("year")] int Year,
			[property: JsonPropertyName("duration")] int? Duration,
			[property: JsonPropertyName("Genre")] IReadOnlyList<Genre>? Genres,
			[property: JsonPropertyName("originallyAvailableAt")] string? OriginallyAvailableAt,
			[property: JsonPropertyName("childCount")] int? SeasonCount,
			[property: JsonPropertyName("leafCount")] int? EpisodeCount
		)
		{
			private MediaType MediaType => MediaType.Parse(Type);
			public PlexId PlexId => PlexId.From(RatingKey, MediaType);
		}

		public sealed record Genre([property: JsonPropertyName("tag")] string Tag);

		public sealed record Root([property: JsonPropertyName("MediaContainer")] MediaContainer MediaContainer);

		public sealed record SearchResult(
			[property: JsonPropertyName("size")] int Size,
			[property: JsonPropertyName("SearchResult")] IReadOnlyList<NestedSearchResult> NestedSearchResults
		);

		public sealed record NestedSearchResult([property: JsonPropertyName("Metadata")] Metadata Metadata);
	}

	internal static class Auth
	{
		public sealed record Root(
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

	internal static class User
	{
		public sealed record Root(
			[property: JsonPropertyName("uuid")] string? Uuid,
			[property: JsonPropertyName("username")] string Username,
			[property: JsonPropertyName("title")] string Title,
			[property: JsonPropertyName("thumb")] string? Thumb,
			[property: JsonPropertyName("email")] string? Email,
			[property: JsonPropertyName("friendlyName")] string? FriendlyName,
			[property: JsonPropertyName("subscriptionDescription")] string? SubscriptionDescription,
			[property: JsonPropertyName("subscription")] Subscription? Subscription,
			[property: JsonPropertyName("joinedAt")] long? JoinedAt
		);

		public sealed record Subscription(
			[property: JsonPropertyName("active")] bool Active,
			[property: JsonPropertyName("status")] string? Status,
			[property: JsonPropertyName("plan")] string? Plan
		);
	}
}
