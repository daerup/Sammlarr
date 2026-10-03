using System.Globalization;
using System.Text.Json;
using API.Tmdb.Types;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Trending;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;

namespace API.Tmdb;

internal sealed class TmdbTrendingService(TmdbClient tmdbClient, TmdbArtwork artwork, IMediaService media, IApiKeyService apiKeys) : ITrendingService
{
	public async Task<PagedResult<MediaLookup>> GetTrendingAsync(int page, CancellationToken cancellationToken = default)
	{
		var formattedEndpoint = string.Format(TmdbEndpoints.Trending, page);
		var apiKey = await apiKeys
			.GetAsync(ApiKeyProvider.Tmdb, cancellationToken)
			.GetOrThrow(_ => new InvalidOperationException("No TMDB API key is configured. Add one in Settings."));
		var response = await tmdbClient.SendAsync(HttpMethod.Get, formattedEndpoint, apiKey, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"TMDB trending request failed with status {(int)response.StatusCode}.");
		}
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = JsonSerializer.Deserialize<TmdbTypes.Trending.Root>(stream) ?? throw new InvalidOperationException("TMDB trending response was empty.");

		var finalItems = new MediaLookup[result.Results.Count];
		var indexed = result.Results.Index().ToList();

		var unresolvable = indexed.Where(x => x.Item.MediaType is MediaType.Unknown).ToList();
		unresolvable.ForEach(r => finalItems[r.Index] = Found(r.Item).Unmatched());

		var resolvable = indexed.Except(unresolvable).ToList();
		var requests = resolvable.Select(x => new IdentifyRequest([x.Item.TmdbId], BuildHints(x.Item)));
		var pipeline = media.IdentifyAsync(requests, cancellationToken);

		await foreach (var (identity, entry) in pipeline.ZipAwait(resolvable, cancellationToken))
		{
			finalItems[entry.Index] = BuildTrending(entry.Item, identity);
		}
		return new PagedResult<MediaLookup>(finalItems, result.TotalPages);
	}

	private static Some<IdentifyHints> BuildHints(TmdbTypes.Trending.Result response) =>
		new IdentifyHints
		{
			Title = response.DisplayTitle,
			Year = response.AvailableYear,
			MediaType = response.MediaType,
		};

	private MediaLookup BuildTrending(TmdbTypes.Trending.Result response, Result<MediaIdentity> outcome)
	{
		var found = Found(response);
		return outcome.Map(found.LookUpAs).GetValueOr(found.Unmatched());
	}

	private FoundMedia Found(TmdbTypes.Trending.Result response) => new([response.TmdbId], BuildDetails(response));

	private MediaDetails BuildDetails(TmdbTypes.Trending.Result response)
	{
		return MediaDetails.Empty(response.MediaType) with
		{
			Title = response.DisplayTitle,
			Summary = response.Overview,
			PosterUrl = artwork.Original(response.PosterPath),
			BackdropUrl = artwork.Original(response.BackdropPath),
			ReleaseDate = ParseReleaseDate(response.AvailableDate),
			Rating = BuildRating(response.VoteAverage),
		};
	}

	private static DateOnly? ParseReleaseDate(string? value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var releaseDate) ? releaseDate : null;

	private static RatingSummary BuildRating(double voteAverage) =>
		voteAverage > 0 ? RatingSummary.From([new MediaRating(MediaProvider.Tmdb, (float)voteAverage)]) : RatingSummary.None;
}
