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

	public async Task<Optional<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		var materialized = candidates.ToList();
		foreach (var plexId in materialized.OfType<PlexId>())
		{
			return await Some.Of(plexApi.LookUpExternalsAsync(plexId, cancellationToken));
		}

		return await hints
			.Map(h => ResolveByTitleAsync(h.Title, h.MediaType, materialized, cancellationToken))
			.Or(() => ResolveByTmdbTitleAsync(materialized, cancellationToken));
	}

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveByTitleAsync(
		string title,
		MediaType mediaType,
		List<IExternalMediaId> candidates,
		CancellationToken cancellationToken
	)
	{
		var searchResults = await plexApi.SearchAsync(title, [mediaType], cancellationToken);
		var match = await searchResults
			.Take(MaxCandidateLookups)
			.ToAsyncEnumerable()
			.SelectAwait(
				async r =>
				{
					var plexExternal = r.Externals.OfType<PlexId>().FirstOrDefault();
					return plexExternal == default ? null : await plexApi.LookUpExternalsAsync(plexExternal, cancellationToken);
				},
				LookupConcurrency,
				cancellationToken
			)
			.FirstOrDefaultAsync(externals => externals is not null && ContainsAnyCandidate(externals, candidates), cancellationToken);

		return Optional.Of(match);
	}

	private async Task<IReadOnlyList<IExternalMediaId>> ConvertFromPlexIdAsync(PlexId plexId, CancellationToken cancellationToken) =>
		await plexApi.LookUpExternalsAsync(plexId, cancellationToken);

	private Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveByTmdbTitleAsync(List<IExternalMediaId> candidates, CancellationToken cancellationToken) =>
		candidates.FirstSomeAsync(candidate =>
			Optional.Of(GetOriginalTitleAsync(candidate, cancellationToken)).Map(title => ResolveByTitleAsync(title, candidate.Type, candidates, cancellationToken))
		);

	private async Task<string?> GetOriginalTitleAsync(IExternalMediaId candidate, CancellationToken cancellationToken) =>
		candidate switch
		{
			TmdbId tmdbId => (await tmdbApi.GetDetailAsync(tmdbId, cancellationToken))?.DisplayOriginalTitle,
			ImdbId imdbId => (await tmdbApi.FindByImdbIdAsync(imdbId, cancellationToken))?.DisplayOriginalTitle,
			_ => null,
		};

	private static bool ContainsAnyCandidate(IEnumerable<IExternalMediaId> externals, IEnumerable<IExternalMediaId> candidates) =>
		candidates.Any(externals.Contains);
}
