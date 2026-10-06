using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using API.Plex.Types;
using Contracts.Media;
using Contracts.Media.Search;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.Logging;

namespace API.Plex;

internal class PlexApi(PlexClient plexClient, PlexUrls urls, ILogger<PlexApi> logger) : IMediaSearchProvider, IDetailsEnricher
{
	private const int SearchLimit = 30;

	public MediaFields Fills { get; } =
		MediaField.Title
		| MediaField.Tagline
		| MediaField.Summary
		| MediaField.Poster
		| MediaField.Backdrop
		| MediaField.ReleaseDate
		| MediaField.Genres
		| MediaField.Runtime
		| MediaField.Seasons
		| MediaField.Episodes
		| MediaField.RuntimePerEpisode
		| MediaFields.Rated(MediaProvider.Plex.SuppliedRatingSources());

	public IReadOnlyList<MediaProvider> Accepts { get; } = [MediaProvider.Plex];

	private readonly ConcurrentDictionary<string, Lazy<Task<Result<FoundMedia>>>> _byRatingKey = new();

	private void EnsureUsable(HttpResponseMessage response, string endpoint)
	{
		if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)response.StatusCode >= 500)
		{
			logger.LogWarning("Plex lookup was rate limited or unavailable ({StatusCode}) on {Endpoint}", (int)response.StatusCode, endpoint);
			throw new RemoteUnavailableException(RemoteService.Plex);
		}

		response.EnsureSuccessStatusCode();
	}

	public Task<Result<FoundMedia>> LookUpAsync(IExternalMediaId mediaId, CancellationToken cancellationToken = default)
	{
		return mediaId.Require<PlexId>($"PlexApi only supports {nameof(PlexId)} lookups.").Map(plexId => ByIdAsync(plexId, cancellationToken));
	}

	public async Task<IReadOnlyList<FoundMedia>> SearchAsync(
		string title,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var searchTypes = string.Join(",", mediaTypes.Select(ToPlexMediaType));
		var endpoint = urls.Search(searchTypes, title, SearchLimit);
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		EnsureUsable(response, endpoint);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = (await JsonSerializer.DeserializeAsync<PlexTypes.Search.Root>(stream, cancellationToken: cancellationToken))!;
		if (result.MediaContainer.Size is 1 && result.MediaContainer.SearchResults[0].Size is 0)
		{
			return [];
		}

		return result
			.MediaContainer.SearchResults.SelectMany(c => c.NestedSearchResult)
			.Select(s => s.Metadata.ToFound())
			.DistinctBy(r => r.Externals.OfType<PlexId>().FirstOrDefault())
			.ToList();
	}

	internal Task<Result<IReadOnlyList<IExternalMediaId>>> LookUpExternalsAsync(PlexId plexId, CancellationToken cancellationToken = default)
	{
		return ByIdAsync(plexId, cancellationToken).Map<FoundMedia, IReadOnlyList<IExternalMediaId>>(found => [.. found.Externals]);
	}

	internal Task<Result<FoundMedia>> ByIdAsync(PlexId plexId, CancellationToken cancellationToken = default)
	{
		return _byRatingKey.GetOrAdd(plexId.Value, key => new Lazy<Task<Result<FoundMedia>>>(() => FetchOnceAsync(key, plexId))).Value.WaitAsync(cancellationToken);
	}

	private async Task<Result<FoundMedia>> FetchOnceAsync(string key, PlexId plexId)
	{
		try
		{
			return await FetchByIdAsync(plexId, CancellationToken.None);
		}
		catch (Exception)
		{
			_byRatingKey.TryRemove(key, out _);
			throw;
		}
	}

	private async Task<Result<FoundMedia>> FetchByIdAsync(PlexId plexId, CancellationToken cancellationToken)
	{
		var endpoint = urls.RatingKeyMetadata(plexId.Value);
		using var response = await plexClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		EnsureUsable(response, endpoint);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await JsonSerializer.DeserializeAsync<PlexTypes.ById.Root>(stream, cancellationToken: cancellationToken);
		var reason = $"Plex returned no metadata for ratingKey {plexId.Value}.";
		return Result.WhenNotNull(result?.MediaContainer?.Metadata, reason).When(metadata => metadata.Count > 0, reason).Map(metadata => metadata[0].ToFound());
	}

	private static string ToPlexMediaType(MediaType mediaType) =>
		mediaType switch
		{
			MediaType.Movie => "movies",
			MediaType.Show => "tv",
			_ => throw new ArgumentOutOfRangeException(nameof(mediaType), $"Unsupported media type: {mediaType}"),
		};
}

file static class PlexMetadataMappingExtensions
{
	public static FoundMedia ToFound(this PlexTypes.Search.Metadata result) => new([result.PlexId], Details(result));

	public static FoundMedia ToFound(this PlexTypes.ById.Metadata result)
	{
		return new([.. BuildExternals(result.PlexId, result.TmdbIds, result.ImdbIds, result.TvdbIds)], Details(result));
	}

	private static MediaDetails Details(PlexTypes.ById.Metadata result)
	{
		return Shape(result.Type, ProviderValues.Milliseconds(result.Duration), result.SeasonCount, result.EpisodeCount) with
		{
			Title = ProviderValues.Text(result.Title),
			Tagline = ProviderValues.Text(result.Tagline),
			Summary = ProviderValues.Text(result.Summary),
			PosterUrl = NormalizePlexImageUrl(result.Poster),
			BackdropUrl = NormalizePlexImageUrl(result.Art),
			ReleaseDate = ProviderValues.Date(result.OriginallyAvailableAt),
			Genres = ProviderValues.Genres(result.Genre?.Select(genre => genre.Tag)),
			Ratings = Ratings(result),
		};
	}

	private static MediaRatings Ratings(PlexTypes.ById.Metadata result)
	{
		return MediaRatings.From(
			(result.Ratings ?? []).Select(entry => SourceOf(entry).Map(source => source.Rate((float)entry.Value, VotesOf(source, result)))).WhereSome()
		);
	}

	private static Optional<RatingSource> SourceOf(PlexTypes.ById.Rating entry)
	{
		return (entry.Image.Split("://")[0], entry.Type) switch
		{
			("imdb", _) => Some.Of(RatingSource.Imdb),
			("themoviedb", _) => Some.Of(RatingSource.Tmdb),
			("rottentomatoes", "critic") => Some.Of(RatingSource.RottenTomatoesCritics),
			("rottentomatoes", "audience") => Some.Of(RatingSource.RottenTomatoesAudience),
			_ => new None(),
		};
	}

	private static Optional<int> VotesOf(RatingSource source, PlexTypes.ById.Metadata result)
	{
		return Optional.Of(ProviderValues.Count(result.ImdbRatingCount)).When(source == RatingSource.Imdb);
	}

	private static MediaDetails Details(PlexTypes.Search.Metadata result)
	{
		return Shape(result.Type, ProviderValues.Milliseconds(result.Duration), result.SeasonCount, result.EpisodeCount) with
		{
			Title = ProviderValues.Text(result.Title),
			PosterUrl = NormalizePlexImageUrl(result.Thumb),
			BackdropUrl = NormalizePlexImageUrl(result.Art),
			ReleaseDate = ProviderValues.Date(result.OriginallyAvailableAt) ?? ProviderValues.YearStart(result.Year),
			Genres = ProviderValues.Genres(result.Genre?.Select(genre => genre.Tag)),
		};
	}

	private static MediaDetails Shape(string type, TimeSpan? duration, int? seasons, int? episodes)
	{
		return ParseMediaType(type) switch
		{
			MediaType.Movie => new MovieDetails { Runtime = duration },
			MediaType.Show => new ShowDetails
			{
				Seasons = ProviderValues.Count(seasons),
				Episodes = ProviderValues.Count(episodes),
				RuntimePerEpisode = duration,
			},
			MediaType.Unknown => new UnknownDetails(),
		};
	}

	private static List<IExternalMediaId> BuildExternals(PlexId plex, IEnumerable<TmdbId> tmdb, IEnumerable<ImdbId> imdb, IEnumerable<TvdbId> tvdb)
	{
		var set = new List<IExternalMediaId> { plex };
		set.AddRange(tmdb.Cast<IExternalMediaId>());
		set.AddRange(imdb.Cast<IExternalMediaId>());
		set.AddRange(tvdb.Cast<IExternalMediaId>());
		return set;
	}

	private static MediaType ParseMediaType(string value)
	{
		return value.ToLowerInvariant() switch
		{
			"movie" => MediaType.Movie,
			"show" => MediaType.Show,
			_ => MediaType.Unknown,
		};
	}

	private static string? NormalizePlexImageUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return null;
		}

		if (Uri.TryCreate(url, UriKind.Absolute, out _))
		{
			return url;
		}

		return $"https://metadata.provider.plex.tv{url}";
	}
}
