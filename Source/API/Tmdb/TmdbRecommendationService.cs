using System.Text.Json;
using API.TMDB.Types;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Trending;
using Contracts.Settings;
using Shared.Options;
using Shared.Telemetry;

namespace API.TMDB;

internal class TmdbRecommendationService(
	TmdbClient tmdbClient,
	TmdbOptions options,
	IMediaIdResolver mediaIdResolver,
	IProviderConfigRepository providerConfigRepository
) : IMediaRecommendationService
{
	public async Task<TrendingPageResult> GetTrendingAsync(int page, CancellationToken cancellationToken = default)
	{
		using var activity = SammlarrTelemetry.StartTrendingLoad(page);
		const string endpoint = "trending/all/week?page={0}";
		var formattedEndpoint = string.Format(endpoint, page);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, formattedEndpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Trending.Root>(stream) ?? throw new InvalidOperationException("TMDB trending response was empty.");
		var settings = await providerConfigRepository.GetAsync(cancellationToken);
		var watchlistProvider = settings.WatchlistProvider.AsMediaProvider();
		var tasks = result.Results.Select(r => BuildTrendingMediaAsync(r, watchlistProvider, cancellationToken).AsTask());
		var awaited = await Task.WhenAll(tasks);
		var items = awaited.Where(m => m is not null).Cast<TmdbTrendingMedia>().ToList();
		return new TrendingPageResult(items, result.TotalPages);
	}

	private async ValueTask<TmdbTrendingMedia?> BuildTrendingMediaAsync(
		TmdbTypes.Trending.Result response,
		MediaProvider watchlistMediaProvider,
		CancellationToken cancellationToken
	)
	{
		if (response.MediaType is MediaType.Unknown)
		{
			return null;
		}
		var resolvedResult = await mediaIdResolver.ResolveAsync(
			[response.TmdbId],
			watchlistMediaProvider,
			new Some<ResolutionHints>(
				new ResolutionHints
				{
					Title = response.DisplayTitle,
					Year = DateTimeOffset.TryParse(response.AvailableDate, out var d) ? d.Year : 0,
					MediaType = response.MediaType,
				}
			),
			cancellationToken
		);
		if (resolvedResult is not Resolved resolvedId)
		{
			return null;
		}

		var releaseDate = string.IsNullOrEmpty(response.AvailableDate) ? "1900-01-01T00:00:00Z" : response.AvailableDate;
		return new TmdbTrendingMedia
		{
			SourceId = response.TmdbId,
			WatchlistProviderId = resolvedId.Id,
			MediaType = response.MediaType,
			Title = response.DisplayTitle,
			Description = response.Overview,
			PosterUrl = options.ImageBaseUrl + response.PosterPath,
			BackdropUrl = options.ImageBaseUrl + response.BackdropPath,
			Rating = (float)response.VoteAverage,
			ReleaseDate = DateTimeOffset.Parse(releaseDate),
		};
	}
}
