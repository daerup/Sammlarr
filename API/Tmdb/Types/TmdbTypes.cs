using System.Globalization;
using System.Text.Json.Serialization;
using Domain.Media;

namespace API.Tmdb.Types;

internal static class TmdbTypes
{
	private static DateOnly? ReleasedOn(MediaType type, string? releaseDate, string? firstAirDate)
	{
		var (first, second) = type is MediaType.Show ? (firstAirDate, releaseDate) : (releaseDate, firstAirDate);
		return ProviderValues.Date(first) ?? ProviderValues.Date(second);
	}

	internal static class Lists
	{
		public sealed record Item(
			[property: JsonPropertyName("id")] int Id,
			[property: JsonPropertyName("media_type")] string? Type,
			[property: JsonPropertyName("title")] string? Title,
			[property: JsonPropertyName("name")] string? Name,
			[property: JsonPropertyName("release_date")] string? ReleaseDate,
			[property: JsonPropertyName("first_air_date")] string? FirstAirDate
		)
		{
			public string? DisplayTitle => ProviderValues.Text(Title) ?? ProviderValues.Text(Name);

			public DateOnly? Released(MediaType type) => ReleasedOn(type, ReleaseDate, FirstAirDate);
		}

		public sealed record Page(
			[property: JsonPropertyName("items")] IReadOnlyList<Item>? Items,
			[property: JsonPropertyName("results")] IReadOnlyList<Item>? Results,
			[property: JsonPropertyName("total_pages")] int? TotalPages
		)
		{
			public IReadOnlyList<Item> Entries => Results ?? Items ?? throw new InvalidDataException("TMDB answered a list without its items.");
		}
	}

	internal static class Trending
	{
		public sealed record Result(
			[property: JsonPropertyName("adult")] bool Adult,
			[property: JsonPropertyName("backdrop_path")] string BackdropPath,
			[property: JsonPropertyName("id")] int Id,
			[property: JsonPropertyName("title")] string? Title,
			[property: JsonPropertyName("name")] string? Name,
			[property: JsonPropertyName("original_title")] string? OriginalTitle,
			[property: JsonPropertyName("original_name")] string? OriginalName,
			[property: JsonPropertyName("overview")] string Overview,
			[property: JsonPropertyName("poster_path")] string PosterPath,
			[property: JsonPropertyName("media_type")] string Type,
			[property: JsonPropertyName("original_language")] string OriginalLanguage,
			[property: JsonPropertyName("genre_ids")] IReadOnlyList<int> GenreIds,
			[property: JsonPropertyName("popularity")] double Popularity,
			[property: JsonPropertyName("release_date")] string? ReleaseDate,
			[property: JsonPropertyName("first_air_date")] string? FirstAirDate,
			[property: JsonPropertyName("video")] bool Video,
			[property: JsonPropertyName("vote_average")] double VoteAverage,
			[property: JsonPropertyName("vote_count")] int VoteCount
		)
		{
			public MediaType MediaType => MediaType.Parse(Type);
			public TmdbId TmdbId => TmdbId.From(Id.ToString(CultureInfo.InvariantCulture), MediaType);

			public string? DisplayTitle => ProviderValues.Text(Title) ?? ProviderValues.Text(Name);
			public DateOnly? Released => ReleasedOn(MediaType, ReleaseDate, FirstAirDate);
		}

		public sealed record Root(
			[property: JsonPropertyName("page")] int Page,
			[property: JsonPropertyName("results")] IReadOnlyList<Result> Results,
			[property: JsonPropertyName("total_pages")] int TotalPages,
			[property: JsonPropertyName("total_results")] int TotalResults
		);
	}

	public sealed record Details(
		[property: JsonPropertyName("id")] int Id,
		[property: JsonPropertyName("original_title")] string? OriginalTitle,
		[property: JsonPropertyName("original_name")] string? OriginalName,
		[property: JsonPropertyName("release_date")] string? ReleaseDate,
		[property: JsonPropertyName("first_air_date")] string? FirstAirDate,
		[property: JsonPropertyName("title")] string? Title,
		[property: JsonPropertyName("name")] string? Name,
		[property: JsonPropertyName("media_type")] string Type,
		[property: JsonPropertyName("tagline")] string? Tagline = null,
		[property: JsonPropertyName("overview")] string? Overview = null,
		[property: JsonPropertyName("poster_path")] string? PosterPath = null,
		[property: JsonPropertyName("backdrop_path")] string? BackdropPath = null,
		[property: JsonPropertyName("runtime")] int? Runtime = null,
		[property: JsonPropertyName("genres")] IReadOnlyList<Genre>? Genres = null,
		[property: JsonPropertyName("number_of_seasons")] int? NumberOfSeasons = null,
		[property: JsonPropertyName("number_of_episodes")] int? NumberOfEpisodes = null,
		[property: JsonPropertyName("episode_run_time")] IReadOnlyList<int>? EpisodeRunTime = null,
		[property: JsonPropertyName("vote_average")] double? VoteAverage = null,
		[property: JsonPropertyName("vote_count")] int? VoteCount = null,
		[property: JsonPropertyName("external_ids")] ExternalIds? ExternalIds = null
	)
	{
		public DateOnly? ReleasedAs(MediaType type) => ReleasedOn(type, ReleaseDate, FirstAirDate);

		public MediaType MediaType => MediaType.Parse(Type);
	}

	public sealed record Genre([property: JsonPropertyName("id")] int Id, [property: JsonPropertyName("name")] string Name);

	public sealed record Find(
		[property: JsonPropertyName("movie_results")] IReadOnlyList<FindItem> MovieResults,
		[property: JsonPropertyName("tv_results")] IReadOnlyList<FindItem> TvResults
	);

	public sealed record ExternalIds([property: JsonPropertyName("imdb_id")] string? ImdbId, [property: JsonPropertyName("tvdb_id")] int? TvdbId)
	{
		public IEnumerable<IExternalMediaId> Ids(MediaType type)
		{
			if (!string.IsNullOrWhiteSpace(ImdbId))
			{
				yield return Domain.Media.ImdbId.From(ImdbId, type);
			}

			if (TvdbId is > 0)
			{
				yield return Domain.Media.TvdbId.From(TvdbId.Value.ToString(CultureInfo.InvariantCulture), type);
			}
		}
	}

	public sealed record FindItem(
		[property: JsonPropertyName("id")] int Id,
		[property: JsonPropertyName("original_title")] string? OriginalTitle,
		[property: JsonPropertyName("original_name")] string? OriginalName,
		[property: JsonPropertyName("release_date")] string? ReleaseDate,
		[property: JsonPropertyName("first_air_date")] string? FirstAirDate,
		[property: JsonPropertyName("title")] string? Title,
		[property: JsonPropertyName("name")] string? Name
	)
	{
		public DateOnly? ReleasedAs(MediaType type) => ReleasedOn(type, ReleaseDate, FirstAirDate);
	}
}
