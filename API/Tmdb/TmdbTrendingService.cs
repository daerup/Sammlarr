using System.Text.Json;
using API.Tmdb.Types;
using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Conversions;
using Contracts.Media.Trending;
using Domain.Media;
using Domain.Providers;

namespace API.Tmdb;

internal sealed class TmdbTrendingService(TmdbClient tmdbClient, TmdbArtwork artwork, IMediaService media, IApiKeyService apiKeys) : ITrendingService
{
	public async Task<PagedResult<MediaMatchResult>> GetTrendingAsync(int page, CancellationToken cancellationToken = default)
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

		var finalItems = new MediaMatchResult[result.Results.Count];
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
		var stored = await media.GetStoredRatingsAsync(finalItems.Select(item => item.MatchedId).WhereSome(), cancellationToken);
		return new PagedResult<MediaMatchResult>([.. finalItems.Select(item => WithStoredRatings(item, stored))], result.TotalPages);
	}

	private static MediaMatchResult WithStoredRatings(MediaMatchResult item, IReadOnlyDictionary<MediaId, RatingSummary> stored)
	{
		return item switch
		{
			MatchedMedia matched => stored
				.GetValueOrNone(matched.Identity.Id)
				.Map(ratings => new MediaMatchResult(matched with { Details = matched.Details with { Rating = matched.Details.Rating.FillFrom(ratings) } }))
				.GetValueOr(item),
			UnmatchedMedia => item,
		};
	}

	private static Optional<IdentifyHints> BuildHints(TmdbTypes.Trending.Result response)
	{
		return Optional
			.Of(response.DisplayTitle)
			.Map(title => new IdentifyHints
			{
				Title = title,
				Year = response.Released?.Year ?? 0,
				MediaType = response.MediaType,
			});
	}

	private MediaMatchResult BuildTrending(TmdbTypes.Trending.Result response, Result<MediaIdentity> outcome)
	{
		var found = Found(response);
		return outcome.Map(found.MatchTo).GetValueOr(found.Unmatched());
	}

	private FoundMedia Found(TmdbTypes.Trending.Result response) => TmdbFound.Of(response, artwork);
}
