using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using API.Tmdb.Types;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Trending;
using Contracts.Settings;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Shared.Options;

namespace API.Tmdb;

internal sealed class TmdbTrendingService(
	TmdbClient tmdbClient,
	TmdbOptions options,
	IMediaService media,
	IProviderConfigRepository providerConfigRepository,
	IApiKeyConfigRepository apiKeyConfigRepository
) : ITrendingService
{
	public async Task<PagedResult<MediaLookup>> GetTrendingAsync(int page, CancellationToken cancellationToken = default)
	{
		var formattedEndpoint = string.Format(TmdbEndpoints.Trending, page);
		var apiKey = await apiKeyConfigRepository
			.GetApiKeyAsync(ApiKeyProvider.Tmdb, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No TMDB API key is configured. Add one in Settings → API Keys."));
		var response = await tmdbClient.SendAsync(HttpMethod.Get, formattedEndpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"TMDB trending request failed with status {(int)response.StatusCode}.");
		}
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Trending.Root>(stream) ?? throw new InvalidOperationException("TMDB trending response was empty.");
		var settings = await providerConfigRepository.GetAsync(cancellationToken);

		var finalItems = new MediaLookup[result.Results.Count];
		var indexed = result.Results.Index().ToList();

		var unresolvable = indexed.Where(x => x.Item.MediaType is MediaType.Unknown).ToList();
		unresolvable.ForEach(r => finalItems[r.Index] = BuildUnmatched(r.Item));

		var resolvable = indexed.Except(unresolvable).ToList();
		var requests = resolvable.Select(x => new ResolveRequest([x.Item.TmdbId], settings.WatchlistProvider.AsMediaProvider(), BuildHints(x.Item)));
		var pipeline = media.IdentifyAsync(requests, cancellationToken);

		await foreach (var (identity, entry) in pipeline.ZipAwait(resolvable, cancellationToken))
		{
			finalItems[entry.Index] = BuildTrending(entry.Item, identity);
		}
		return new PagedResult<MediaLookup>(finalItems, result.TotalPages);
	}

	private static Some<ResolutionHints> BuildHints(TmdbTypes.Trending.Result response) =>
		new ResolutionHints
		{
			Title = response.DisplayTitle,
			Year = DateTimeOffset.TryParse(response.AvailableDate, out var d) ? d.Year : 0,
			MediaType = response.MediaType,
		};

	private MediaLookup BuildTrending(TmdbTypes.Trending.Result response, Result<MediaIdentity> outcome) =>
		outcome.Match(identity => IdentifiedMedia.From(BuildPartial(response, identity)), _ => BuildUnmatched(response));

	private IPartialMedia BuildPartial(TmdbTypes.Trending.Result response, MediaIdentity identity) =>
		response.MediaType switch
		{
			MediaType.Show => new PartialShow
			{
				Identity = identity,
				Title = response.DisplayTitle,
				Summary = response.Overview,
				PosterUrl = options.ImageBaseUrl + response.PosterPath,
				BackdropUrl = options.ImageBaseUrl + response.BackdropPath,
				ReleaseDate = ParseReleaseDate(response.AvailableDate),
				Rating = BuildRating(response.VoteAverage),
			},
			MediaType.Movie => new PartialMovie
			{
				Identity = identity,
				Title = response.DisplayTitle,
				Summary = response.Overview,
				PosterUrl = options.ImageBaseUrl + response.PosterPath,
				BackdropUrl = options.ImageBaseUrl + response.BackdropPath,
				ReleaseDate = ParseReleaseDate(response.AvailableDate),
				Rating = BuildRating(response.VoteAverage),
			},
			MediaType.Unknown => throw new UnreachableException(),
		};

	private MediaLookup BuildUnmatched(TmdbTypes.Trending.Result response) =>
		new UnmatchedMedia
		{
			Externals = [response.TmdbId],
			Title = response.DisplayTitle,
			Summary = response.Overview,
			PosterUrl = options.ImageBaseUrl + response.PosterPath,
			BackdropUrl = options.ImageBaseUrl + response.BackdropPath,
			ReleaseDate = ParseReleaseDate(response.AvailableDate),
			Rating = BuildRating(response.VoteAverage),
		};

	private static DateOnly? ParseReleaseDate(string? value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : null;

	private static RatingSummary BuildRating(double voteAverage) =>
		voteAverage > 0 ? RatingSummary.From([new MediaRating(RatingProvider.Tmdb, (float)voteAverage)]) : RatingSummary.None;
}
