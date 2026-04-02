using System.Text.Json;
using API.TMDB.Types;
using Contracts.Media;
using Shared;
using Shared.DI;
using Shared.Options;

namespace API.TMDB;

public class TmdbApi(TmdbClient tmdbClient, TmdbOptions options, IKeyedServiceIndex<MediaProvider, IMediaIdConverter> converterIndex)
	: IMediaRecommendationService
{
	public async Task<TrendingPageResult> GetTrendingAsync(int page, CancellationToken cancellationToken = default)
	{
		const string endpoint = "trending/all/week?page={0}";
		var formattedEndpoint = string.Format(endpoint, page);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, formattedEndpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Trending.Root>(stream) ?? throw new InvalidOperationException("TMDB trending response was empty.");
		var tasks = result.Results.Select(r => BuildTrendingMediaAsync(r).AsTask());
		var awaited = await Task.WhenAll(tasks);
		var items = awaited.Where(m => m is not null).Cast<TmdbTrendingMedia>().ToList();
		return new TrendingPageResult(items, result.TotalPages);
	}

	public async Task<TmdbTypes.Details> GetDetailAsync(TmdbId id, MediaType mediaType, CancellationToken cancellationToken = default)
	{
		var endpoint = $"{ToTmdbMediaType(mediaType)}/{id}";
		var response = await tmdbClient.SendAsync(HttpMethod.Get, endpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Details>(stream);
		return result!;
	}

	private static string ToTmdbMediaType(MediaType mediaType)
	{
		return mediaType switch
		{
			MediaType.Movie => "movie",
			MediaType.Show => "tv",
			_ => throw new ArgumentOutOfRangeException(nameof(mediaType), $"Unsupported media type: {mediaType}"),
		};
	}

	private async ValueTask<TmdbTrendingMedia?> BuildTrendingMediaAsync(TmdbTypes.Trending.Result response)
	{
		var mediaType = MediaType.FromString(response.MediaType);
		var externalId = await converterIndex[MediaProvider.Tmdb].ConvertToOrDefaultAsync<PlexId>(response.TmdbId, mediaType);
		if (externalId is null)
		{
			return null!;
		}
		return new TmdbTrendingMedia
		{
			SourceId = response.TmdbId,
			ExternalId = externalId,
			Provider = MediaProvider.Tmdb,
			MediaType = mediaType,
			Title = response.DisplayTitle,
			Description = response.Overview,
			PosterUrl = options.ImageBaseUrl + response.PosterPath,
			BackdropUrl = options.ImageBaseUrl + response.BackdropPath,
			Rating = (float)response.VoteAverage,
			ReleaseDate = DateTimeOffset.Parse(response.AvailableDate ?? "1900-01-01T00:00:00Z"),
		};
	}
}
