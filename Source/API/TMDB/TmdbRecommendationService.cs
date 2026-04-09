// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Text.Json;
using API.TMDB.Types;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Trending;
using Shared.Options;

namespace API.TMDB;

internal class TmdbRecommendationService(TmdbClient tmdbClient, TmdbOptions options, IMediaIdResolver mediaIdResolver) : IMediaRecommendationService
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

    private async ValueTask<TmdbTrendingMedia?> BuildTrendingMediaAsync(TmdbTypes.Trending.Result response)
    {
        if (response.MediaType is null)
        {
            return null;
        }

        var resovledId = await mediaIdResolver.ResolveAsync(response.TmdbId, MediaProvider.Plex);
        if (!resovledId.Successful)
        {
            return null!;
        }

        var releaseDate = string.IsNullOrEmpty(response.AvailableDate) ? "1900-01-01T00:00:00Z" : response.AvailableDate;
        return new TmdbTrendingMedia
        {
            SourceId = response.TmdbId,
            WatchlistProviderId = resovledId.Id,
            Provider = MediaProvider.Tmdb,
            MediaType = response.MediaType.Value,
            Title = response.DisplayTitle,
            Description = response.Overview,
            PosterUrl = options.ImageBaseUrl + response.PosterPath,
            BackdropUrl = options.ImageBaseUrl + response.BackdropPath,
            Rating = (float)response.VoteAverage,
            ReleaseDate = DateTimeOffset.Parse(releaseDate),
        };
    }
}
