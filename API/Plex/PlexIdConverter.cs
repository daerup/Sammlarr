using API.Tmdb;
using Contracts;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Shared;

namespace API.Plex;

internal class PlexIdConverter(TmdbApi tmdbApi, PlexApi plexApi) : IExternalIdConverter
{
	private const int LookupConcurrency = 4;
	private const int MaxCandidateLookups = 5;

	public async Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		var materialized = candidates.ToList();
		foreach (var plexId in materialized.OfType<PlexId>())
		{
			return await plexApi.LookUpExternalsAsync(plexId, cancellationToken);
		}

		return await hints
			.Map(h => ResolveByTitleAsync(h.Title, h.MediaType, materialized, cancellationToken))
			.Or(() => ResolveByTmdbTitleAsync(materialized, cancellationToken))
			.ToResult("No Plex id could be resolved for the given candidates.");
	}

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveByTitleAsync(
		string title,
		MediaType mediaType,
		List<IExternalMediaId> candidates,
		CancellationToken cancellationToken
	)
	{
		var searchResults = await plexApi.SearchAsync(title, [mediaType], cancellationToken);
		return await searchResults
			.Take(MaxCandidateLookups)
			.Select(result => result.Externals.OfType<PlexId>().FirstOrDefault())
			.Where(plexId => plexId != default)
			.SelectAwait(async plexId => await plexApi.LookUpExternalsAsync(plexId, cancellationToken).GetValueOr([]), LookupConcurrency, cancellationToken)
			.FirstOrNoneAsync(externals => ContainsAnyCandidate(externals, candidates), cancellationToken);
	}

	private Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveByTmdbTitleAsync(List<IExternalMediaId> candidates, CancellationToken cancellationToken)
	{
		return candidates.FirstSomeAsync(candidate =>
			GetOriginalTitleAsync(candidate, cancellationToken).Map(title => ResolveByTitleAsync(title, candidate.Type, candidates, cancellationToken))
		);
	}

	private async Task<Optional<string>> GetOriginalTitleAsync(IExternalMediaId candidate, CancellationToken cancellationToken) =>
		candidate switch
		{
			TmdbId tmdbId => OriginalTitleOrNone(await tmdbApi.GetDetailAsync(tmdbId, cancellationToken), detail => detail.DisplayOriginalTitle),
			ImdbId imdbId => OriginalTitleOrNone(await tmdbApi.FindByImdbIdAsync(imdbId, cancellationToken), item => item.DisplayOriginalTitle),
			_ => new None(),
		};

	private static Optional<string> OriginalTitleOrNone<T>(Result<T> lookup, Func<T, string> originalTitle)
		where T : notnull => lookup.Match(found => Optional.Of(originalTitle(found)), _ => new None());

	private static bool ContainsAnyCandidate(IEnumerable<IExternalMediaId> externals, IEnumerable<IExternalMediaId> candidates) =>
		candidates.Any(externals.Contains);
}
