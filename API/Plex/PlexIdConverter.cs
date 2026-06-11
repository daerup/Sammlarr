using API.Tmdb;
using Contracts;
using Contracts.Media;
using Contracts.Media.Convertions;
using Shared;

namespace API.Plex;

internal class PlexIdConverter(TmdbApi tmdbApi, PlexApi plexApi) : IExternalIdConverter
{
	private const int LookupConcurrency = 4;

	public async Task<Optional<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		var materialized = candidates.ToList();
		foreach (var plexId in materialized.OfType<PlexId>())
		{
			return Some.Of(await ConvertFromPlexIdAsync(plexId, cancellationToken));
		}

		if (hints is Some<ResolutionHints>(var h))
		{
			var result = await ResolveFromTitleSearchAsync(h.Title, h.MediaType, materialized, cancellationToken);
			if (result is Some<IReadOnlyList<IExternalMediaId>>)
			{
				return result;
			}
		}

		return await TryResolveFromApiTitleSearchAsync(materialized, cancellationToken);
	}

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveFromTitleSearchAsync(
		string title,
		MediaType mediaType,
		List<IExternalMediaId> candidates,
		CancellationToken cancellationToken
	)
	{
		var searchResults = await plexApi.SearchAsync(title, [mediaType], cancellationToken);
		var match = await searchResults
			.ToAsyncEnumerable()
			.SelectAwait(
				async r =>
				{
					var plexExternal = r.Identity.Externals.OfType<PlexId>().FirstOrDefault();
					return plexExternal == default ? null : await plexApi.LookUpExternalsAsync(plexExternal, cancellationToken);
				},
				LookupConcurrency,
				cancellationToken
			)
			.FirstOrDefaultAsync(externals => externals is not null && ContainsAnyCandidate(externals, candidates), cancellationToken);

		return match is null ? new None() : Some.Of(match);
	}

	private async Task<IReadOnlyList<IExternalMediaId>> ConvertFromPlexIdAsync(PlexId plexId, CancellationToken cancellationToken) =>
		await plexApi.LookUpExternalsAsync(plexId, cancellationToken);

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> TryResolveFromApiTitleSearchAsync(
		List<IExternalMediaId> candidates,
		CancellationToken cancellationToken
	)
	{
		foreach (var candidate in candidates)
		{
			var title = await GetOriginalTitleAsync(candidate, cancellationToken);
			if (title is null)
			{
				continue;
			}
			var result = await ResolveFromTitleSearchAsync(title, candidate.Type, candidates, cancellationToken);
			if (result is Some<IReadOnlyList<IExternalMediaId>>)
			{
				return result;
			}
		}

		return new None();
	}

	private async Task<string?> GetOriginalTitleAsync(IExternalMediaId candidate, CancellationToken cancellationToken) =>
		candidate switch
		{
			TmdbId tmdbId => (await tmdbApi.GetDetailAsync(tmdbId, cancellationToken))?.DisplayOriginalTitle,
			ImdbId imdbId => (await tmdbApi.FindByImdbIdAsync(imdbId, cancellationToken))?.DisplayOriginalTitle,
			_ => null,
		};

	private static bool ContainsAnyCandidate(IReadOnlyList<IExternalMediaId> externals, List<IExternalMediaId> candidates) => candidates.Any(externals.Contains);
}
