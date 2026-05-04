using System.Text.Json.Serialization;
using Contracts.Media;

namespace API.TMDB.Types;

internal static class TmdbTypes
{
	internal static class Trending
	{
		public record Result(
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
			public TmdbId TmdbId => TmdbId.From(Id.ToString(), MediaType);

			public string DisplayTitle => Title ?? Name ?? throw new InvalidOperationException("Both Title and Name are null");
			public string DisplayOriginalTitle =>
				OriginalTitle ?? OriginalName ?? throw new InvalidOperationException("Both OriginalTitle and OriginalName are null");
			public string? AvailableDate => FirstAirDate ?? ReleaseDate;
		}

		public record Root(
			[property: JsonPropertyName("page")] int Page,
			[property: JsonPropertyName("results")] IReadOnlyList<Result> Results,
			[property: JsonPropertyName("total_pages")] int TotalPages,
			[property: JsonPropertyName("total_results")] int TotalResults
		);
	}

	public record Details(
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
		[property: JsonPropertyName("episode_run_time")] IReadOnlyList<int>? EpisodeRunTime = null
	)
	{
		public string DisplayTitle => Title ?? Name ?? throw new InvalidOperationException("Both Title and Name are null");
		public string DisplayOriginalTitle =>
			OriginalTitle ?? OriginalName ?? throw new InvalidOperationException("Both OriginalTitle and OriginalName are null for Id: " + Id);
		public string? AvailableDate => FirstAirDate ?? ReleaseDate;

		public MediaType MediaType => MediaType.Parse(Type);
		public TmdbId TmdbId => TmdbId.From(Id.ToString(), MediaType);
	}

	public record Genre([property: JsonPropertyName("id")] int Id, [property: JsonPropertyName("name")] string Name);

	public record Find(
		[property: JsonPropertyName("movie_results")] IReadOnlyList<FindItem> MovieResults,
		[property: JsonPropertyName("tv_results")] IReadOnlyList<FindItem> TvResults
	);

	public record FindItem(
		[property: JsonPropertyName("id")] int Id,
		[property: JsonPropertyName("original_title")] string? OriginalTitle,
		[property: JsonPropertyName("original_name")] string? OriginalName,
		[property: JsonPropertyName("release_date")] string? ReleaseDate,
		[property: JsonPropertyName("first_air_date")] string? FirstAirDate,
		[property: JsonPropertyName("title")] string? Title,
		[property: JsonPropertyName("name")] string? Name
	)
	{
		public string DisplayTitle => Title ?? Name ?? throw new InvalidOperationException("Both Title and Name are null");
		public string DisplayOriginalTitle =>
			OriginalTitle ?? OriginalName ?? throw new InvalidOperationException("Both OriginalTitle and OriginalName are null");
		public string? AvailableDate => FirstAirDate ?? ReleaseDate;
	}
}
