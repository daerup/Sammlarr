using API.TMDB;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Search;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace API.Plex;

internal class PlexIdConverter(TmdbApi tmdbApi, PlexApi plexApi, [FromKeyedServices(MediaSearchProvider.Plex)] IMediaSearchService searchService)
{
	private const int LookupConcurrency = 4;

	public async Task<ConversionResult> ConvertAsync(
		IReadOnlyCollection<IMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		foreach (var plexId in candidates.OfType<PlexId>())
		{
			return await ConvertFromPlexIdAsync(plexId, cancellationToken);
		}

		if (hints is Some<ResolutionHints>(var h))
		{
			var result = await ResolveFromTitleSearchAsync(h.Title, h.MediaType, candidates, cancellationToken);
			if (result is Resolved)
			{
				return result;
			}
		}

		return await TryResolveFromApiTitleSearchAsync(candidates, cancellationToken);
	}

	private async Task<ConversionResult> ResolveFromTitleSearchAsync(
		string title,
		MediaType mediaType,
		IReadOnlyCollection<IMediaId> candidates,
		CancellationToken cancellationToken
	)
	{
		var searchResults = await searchService.SearchAsync(title, [mediaType], cancellationToken);
		var result = await searchResults
			.ToAsyncEnumerable()
			.SelectAwait(async r => await plexApi.LookUpAsync(r.SourceId, cancellationToken), LookupConcurrency, cancellationToken)
			.FirstOrDefaultAsync(details => ContainsAnyCandidate(details.AdditionalIds, candidates), cancellationToken);

		return result is not null ? new Resolved(result.SourceId, result.AdditionalIds) : new Unresolved();
	}

	private async Task<ConversionResult> ConvertFromPlexIdAsync(PlexId plexId, CancellationToken cancellationToken)
	{
		var details = await plexApi.LookUpAsync(plexId, cancellationToken);
		return new Resolved(plexId, details.AdditionalIds);
	}

	private async Task<ConversionResult> TryResolveFromApiTitleSearchAsync(IReadOnlyCollection<IMediaId> candidates, CancellationToken cancellationToken)
	{
		foreach (var candidate in candidates)
		{
			var title = await GetOriginalTitleAsync(candidate, cancellationToken);
			if (title is null)
			{
				continue;
			}
			if (await ResolveFromTitleSearchAsync(title, candidate.Type, candidates, cancellationToken) is Resolved resolved)
			{
				return resolved;
			}
		}

		return new Unresolved();
	}

	private async Task<string?> GetOriginalTitleAsync(IMediaId candidate, CancellationToken cancellationToken) =>
		candidate switch
		{
			TmdbId tmdbId => (await tmdbApi.GetDetailAsync(tmdbId, cancellationToken))?.DisplayOriginalTitle,
			ImdbId imdbId => (await tmdbApi.FindByImdbIdAsync(imdbId, cancellationToken))?.DisplayOriginalTitle,
			_ => null,
		};

	private static bool ContainsAnyCandidate(IEnumerable<IMediaId> additionalIds, IEnumerable<IMediaId> candidates)
	{
		var additional = additionalIds.ToList();
		return candidates.Any(additional.Contains);
	}
}
