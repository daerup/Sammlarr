using API.TMDB;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Contracts.Media.Search;
using Microsoft.Extensions.DependencyInjection;

namespace API.Plex;

internal class PlexIdConverter(TmdbApi tmdbApi, PlexApi plexApi, [FromKeyedServices(MediaSearchProvider.Plex)] IMediaSearchService searchService)
{
	public async Task<ConversionResult> ConvertAsync(IReadOnlyCollection<IMediaId> candidates, Optional<ResolutionHints> hints)
	{
		foreach (var plexId in candidates.OfType<PlexId>())
			return await ConvertFromPlexIdAsync(plexId);

		if (hints is Some<ResolutionHints>(var h))
		{
			var result = await ResolveFromTitleSearchAsync(h.Title, h.MediaType, candidates);
			if (result is Resolved)
			{
				return result;
			}
		}

		return await TryResolveFromApiTitleSearchAsync(candidates);
	}

	private async Task<ConversionResult> ResolveFromTitleSearchAsync(string title, MediaType mediaType, IReadOnlyCollection<IMediaId> candidates)
	{
		var searchResults = await searchService.SearchAsync(title, [mediaType]);

		foreach (var plexId in searchResults.Select(result => result.SourceId).OfType<PlexId>())
		{
			var details = await plexApi.LookUpAsync(plexId);
			if (ContainsAnyCandidate(details.AdditionalIds, candidates))
			{
				return new Resolved(details.SourceId, details.AdditionalIds);
			}
		}

		return new Unresolved();
	}

	private async Task<ConversionResult> ConvertFromPlexIdAsync(PlexId plexId)
	{
		var details = await plexApi.LookUpAsync(plexId);
		return new Resolved(plexId, details.AdditionalIds);
	}

	private async Task<ConversionResult> TryResolveFromApiTitleSearchAsync(IReadOnlyCollection<IMediaId> candidates)
	{
		foreach (var candidate in candidates)
		{
			if (candidate is TmdbId tmdbId)
			{
				var details = await tmdbApi.GetDetailAsync(tmdbId);
				return await ResolveFromTitleSearchAsync(details.DisplayOriginalTitle, tmdbId.Type, candidates);
			}
		}

		return new Unresolved();
	}

	private static bool ContainsAnyCandidate(IEnumerable<IMediaId> additionalIds, IReadOnlyCollection<IMediaId> candidates)
	{
		var additional = additionalIds.ToList();
		return candidates.Any(additional.Contains);
	}
}
