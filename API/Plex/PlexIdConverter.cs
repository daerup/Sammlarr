using API.Http;
using Contracts.Caching;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Plex;

internal class PlexIdConverter(PlexApi plexApi, ITypedMemoryCache<IdentifyHints, IReadOnlyList<FoundMedia>> searches) : IExternalIdConverter
{
	private const int LookupConcurrency = 4;
	private const int MaxCandidateLookups = 3;

	private static readonly TimeSpan SearchLifetime = TimeSpan.FromMinutes(10);

	private static readonly ValueSet<MediaProvider> Guids = [MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb];

	private static readonly ById ByRatingKey = new(Accepts: [MediaProvider.Plex], Yields: Guids, Cost: 1);

	private static readonly ByHints Search = new(Yields: Guids, Cost: 1 + MaxCandidateLookups);

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByRatingKey, Search];

	public Task<Answer<IdLookupResult>> LookUpAsync(IdLookupRequest request, CancellationToken cancellationToken = default)
	{
		return LookUpResultAsync(request, cancellationToken).GuardedAsync(RemoteService.Plex);
	}

	private async Task<Result<IdLookupResult>> LookUpResultAsync(IdLookupRequest request, CancellationToken cancellationToken)
	{
		return request switch
		{
			ByIdRequest(var lookup, PlexId plexId) when lookup == ByRatingKey => await ByRatingKeyAsync(plexId, cancellationToken),
			ByHintsRequest(var lookup, var hints, var known) when lookup == Search => await SearchByHintsAsync(hints, known, cancellationToken),
			_ => Error.From("Plex does not offer this lookup."),
		};
	}

	private Task<Result<IdLookupResult>> ByRatingKeyAsync(PlexId plexId, CancellationToken cancellationToken)
	{
		return plexApi.LookUpExternalsAsync(plexId, cancellationToken).Map(Answer);
	}

	private async Task<Result<IdLookupResult>> SearchByHintsAsync(IdentifyHints hints, IReadOnlyList<IExternalMediaId> known, CancellationToken cancellationToken)
	{
		var match = await ResolveByTitleAsync(hints, known, cancellationToken);
		return match.Map(Answer).ToResult("Plex found no title matching the search.");
	}

	private static IdLookupResult Answer(IReadOnlyList<IExternalMediaId> ids) => new(ids, new None());

	private Task<IReadOnlyList<FoundMedia>> CachedSearchAsync(IdentifyHints hints, CancellationToken cancellationToken)
	{
		return searches.GetValueOrNone(hints).Match(Task.FromResult, () => SearchAsync(hints, cancellationToken));
	}

	private async Task<IReadOnlyList<FoundMedia>> SearchAsync(IdentifyHints hints, CancellationToken cancellationToken)
	{
		var hits = await plexApi.SearchAsync(hints.Title, [hints.MediaType], cancellationToken);
		searches.Set(hints, hits, SearchLifetime);
		return hits;
	}

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveByTitleAsync(
		IdentifyHints hints,
		IReadOnlyList<IExternalMediaId> known,
		CancellationToken cancellationToken
	)
	{
		var searchResults = await CachedSearchAsync(hints, cancellationToken);
		var unavailable = false;
		var match = await RankByHints(searchResults, hints.Title, hints.Year)
			.Take(MaxCandidateLookups)
			.Select(result => result.Externals.OfType<PlexId>().FirstOrDefault())
			.Where(plexId => plexId != default)
			.SelectAwait(
				async (plexId, token) =>
				{
					try
					{
						return await plexApi.LookUpExternalsAsync(plexId, token).GetValueOr([]);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						unavailable = true;
						return [];
					}
				},
				LookupConcurrency,
				cancellationToken
			)
			.FirstOrNoneAsync(externals => ContainsAnyCandidate(externals, known), cancellationToken);

		return match.ApplyNone(() => FailWhenUnavailable(unavailable));
	}

	private static void FailWhenUnavailable(bool unavailable)
	{
		if (unavailable)
		{
			throw new RemoteUnavailableException(RemoteService.Plex, sent: true);
		}
	}

	private static IEnumerable<FoundMedia> RankByHints(IReadOnlyList<FoundMedia> results, string title, int year)
	{
		return results.OrderByDescending(result => Score(result, title, year));
	}

	private static int Score(FoundMedia result, string title, int year)
	{
		var (resultTitle, resultYear) = Describe(result.Details);
		var titleMatches = resultTitle is not null && Comparable(resultTitle) == Comparable(title);
		var yearMatches = year > 0 && resultYear == year;
		return (yearMatches ? 2 : 0) + (titleMatches ? 1 : 0);
	}

	private static (string? Title, int? Year) Describe(MediaDetails details) => (details.Title, details.ReleaseDate?.Year);

	private static string Comparable(string value)
	{
		var trimmed = value.Trim();
		var qualifier = trimmed.LastIndexOf('(');
		if (qualifier > 0 && trimmed.EndsWith(')'))
		{
			trimmed = trimmed[..qualifier];
		}
		return new([.. trimmed.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
	}

	private static bool ContainsAnyCandidate(IEnumerable<IExternalMediaId> externals, IEnumerable<IExternalMediaId> candidates) =>
		candidates.Any(externals.Contains);
}
