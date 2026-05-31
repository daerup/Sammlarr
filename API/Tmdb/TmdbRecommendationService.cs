using System.Text.Json;
using API.Tmdb.Types;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Trending;
using Contracts.Settings;
using Shared;
using Shared.Options;
using Shared.Telemetry;

namespace API.Tmdb;

internal class TmdbRecommendationService(TmdbClient tmdbClient, TmdbOptions options, IMediaService media, IProviderConfigRepository providerConfigRepository)
	: IMediaRecommendationService
{
	public async Task<TrendingPageResult> GetTrendingAsync(int page, CancellationToken cancellationToken = default)
	{
		using var activity = SammlarrTelemetry.StartTrendingLoad(page);
		var formattedEndpoint = string.Format(TmdbEndpoints.Trending, page);
		var response = await tmdbClient.SendAsync(HttpMethod.Get, formattedEndpoint, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Trending.Root>(stream) ?? throw new InvalidOperationException("TMDB trending response was empty.");
		var settings = await providerConfigRepository.GetAsync(cancellationToken);

		var finalItems = new ITrendingMedia[result.Results.Count];
		var indexed = result.Results.Index().ToList();

		var unresolvable = indexed.Where(x => x.Item.MediaType is MediaType.Unknown).ToList();
		unresolvable.ForEach(r => finalItems[r.Index] = BuildTrendingMedia(r.Item, Error.From("Unknown media type")));

		var resolvable = indexed.Where(x => x.Item.MediaType is not MediaType.Unknown).ToList();
		var requests = resolvable.Select(x => new ResolveRequest([x.Item.TmdbId], settings.WatchlistProvider.AsMediaProvider(), BuildHints(x.Item)));
		var pipeline = media.IdentifyAsync(requests, cancellationToken);

		await foreach (var (identity, entry) in pipeline.Zip(resolvable, cancellationToken))
		{
			finalItems[entry.Index] = BuildTrendingMedia(entry.Item, identity);
		}
		return new TrendingPageResult(finalItems, result.TotalPages);
	}

	private static Some<ResolutionHints> BuildHints(TmdbTypes.Trending.Result response)
	{
		return new Some<ResolutionHints>(
			new ResolutionHints
			{
				Title = response.DisplayTitle,
				Year = DateTimeOffset.TryParse(response.AvailableDate, out var d) ? d.Year : 0,
				MediaType = response.MediaType,
			}
		);
	}

	private ITrendingMedia BuildTrendingMedia(TmdbTypes.Trending.Result response, Result<Identification> outcome)
	{
		var releaseDate = string.IsNullOrEmpty(response.AvailableDate) ? "1900-01-01T00:00:00Z" : response.AvailableDate;
		var parsedReleaseDate = DateTimeOffset.Parse(releaseDate);
		var posterUrl = options.ImageBaseUrl + response.PosterPath;
		var backdropUrl = options.ImageBaseUrl + response.BackdropPath;

		if (outcome is not Identification identification)
		{
			return new UnresolvedTrendingMedia
			{
				Externals = new List<IExternalMediaId> { response.TmdbId },
				MediaType = response.MediaType,
				Title = response.DisplayTitle,
				Description = response.Overview,
				PosterUrl = posterUrl,
				BackdropUrl = backdropUrl,
				Rating = response.VoteAverage == 0 ? null : (float?)response.VoteAverage,
				ReleaseDate = parsedReleaseDate,
			};
		}

		return new TrendingMedia
		{
			Id = identification.Id,
			Externals = identification.Externals,
			MediaType = response.MediaType,
			Title = response.DisplayTitle,
			Description = response.Overview,
			PosterUrl = posterUrl,
			BackdropUrl = backdropUrl,
			Rating = (float)response.VoteAverage,
			ReleaseDate = parsedReleaseDate,
		};
	}
}
