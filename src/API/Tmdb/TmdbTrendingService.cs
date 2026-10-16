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
		var result = await FetchAsync(page, cancellationToken);
		var finalItems = await MatchAsync(result.Results, cancellationToken);
		var stored = await media.GetStoredRatingsAsync(finalItems.Select(item => item.MatchedId).WhereSome(), cancellationToken);
		return new PagedResult<MediaMatchResult>([.. finalItems.Select(item => WithStoredRatings(item, stored))], result.TotalPages);
	}

	private async Task<TmdbTypes.Trending.Root> FetchAsync(int page, CancellationToken cancellationToken)
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
		return await JsonSerializer.DeserializeAsync<TmdbTypes.Trending.Root>(stream, cancellationToken: cancellationToken)
			?? throw new InvalidOperationException("TMDB trending response was empty.");
	}

	private async Task<MediaMatchResult[]> MatchAsync(IReadOnlyList<TmdbTypes.Trending.Result> results, CancellationToken cancellationToken)
	{
		var finalItems = new MediaMatchResult[results.Count];
		var indexed = results.Index().ToList();

		var unresolvable = indexed.Where(x => x.Item.MediaType is MediaType.Unknown).ToList();
		unresolvable.ForEach(r => finalItems[r.Index] = Found(r.Item).Unmatched());

		var resolvable = indexed.Except(unresolvable).ToList();
		var requests = resolvable.Select(x => new IdentifyRequest([x.Item.TmdbId], BuildHints(x.Item)) { Sources = [RemoteService.Tmdb] });
		var pipeline = media.IdentifyAsync(requests, cancellationToken);

		await foreach (var (identity, entry) in pipeline.ZipAwait(resolvable, cancellationToken))
		{
			finalItems[entry.Index] = BuildTrending(entry.Item, identity);
		}

		return finalItems;
	}

	private static MediaMatchResult WithStoredRatings(MediaMatchResult item, IReadOnlyDictionary<MediaId, MediaRatings> stored)
	{
		return item switch
		{
			MatchedMedia matched => stored
				.GetValueOrNone(matched.Identity.Id)
				.Map(ratings => new MediaMatchResult(matched with { Details = matched.Details with { Ratings = matched.Details.Ratings.FillFrom(ratings) } }))
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
