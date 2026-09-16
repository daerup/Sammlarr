using API.Tmdb;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;

namespace API.Plex;

internal class PlexIdConverter(TmdbApi tmdbApi, PlexApi plexApi) : IExternalIdConverter
{
	private const int LookupConcurrency = 4;
	private const int MaxCandidateLookups = 3;

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
			.Map(h => ResolveByTitleAsync(h.Title, h.Year, h.MediaType, materialized, cancellationToken))
			.Or(() => ResolveByTmdbTitleAsync(materialized, cancellationToken))
			.ToResult("No Plex id could be resolved for the given candidates.");
	}

	private async Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveByTitleAsync(
		string title,
		int year,
		MediaType mediaType,
		List<IExternalMediaId> candidates,
		CancellationToken cancellationToken
	)
	{
		var searchResults = await plexApi.SearchAsync(title, [mediaType], cancellationToken);
		var unavailable = false;
		var match = await RankByHints(searchResults, title, year)
			.Take(MaxCandidateLookups)
			.Select(result => result.Externals.OfType<PlexId>().FirstOrDefault())
			.Where(plexId => plexId != default)
			.SelectAwait(
				async plexId =>
				{
					try
					{
						return await plexApi.LookUpExternalsAsync(plexId, cancellationToken).GetValueOr([]);
					}
					catch (ProviderUnavailableException)
					{
						unavailable = true;
						return (IReadOnlyList<IExternalMediaId>)[];
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						return (IReadOnlyList<IExternalMediaId>)[];
					}
				},
				LookupConcurrency,
				cancellationToken
			)
			.FirstOrNoneAsync(externals => ContainsAnyCandidate(externals, candidates), cancellationToken);

		if (match is None && unavailable)
		{
			throw new ProviderUnavailableException(MediaProvider.Plex);
		}

		return match;
	}

	private Task<Optional<IReadOnlyList<IExternalMediaId>>> ResolveByTmdbTitleAsync(List<IExternalMediaId> candidates, CancellationToken cancellationToken)
	{
		return candidates.FirstSomeAsync(candidate =>
			GetOriginalTitleAsync(candidate, cancellationToken).Map(title => ResolveByTitleAsync(title, 0, candidate.Type, candidates, cancellationToken))
		);
	}

	private static IEnumerable<MediaLookup> RankByHints(IReadOnlyList<MediaLookup> results, string title, int year) =>
		results.OrderByDescending(result => Score(result, title, year));

	private static int Score(MediaLookup result, string title, int year)
	{
		var (resultTitle, resultYear) = Describe(result);
		var titleMatches = resultTitle is not null && Comparable(resultTitle) == Comparable(title);
		var yearMatches = year > 0 && resultYear == year;
		return (yearMatches ? 2 : 0) + (titleMatches ? 1 : 0);
	}

	private static (string? Title, int? Year) Describe(MediaLookup result) =>
		result switch
		{
			IdentifiedMedia identified => (identified.Title, identified.ReleaseDate?.Year),
			UnmatchedMedia unmatched => (unmatched.Title, unmatched.ReleaseDate?.Year),
		};

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
