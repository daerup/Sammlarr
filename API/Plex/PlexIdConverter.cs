using API.Tmdb;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Media.Conversions;
using Domain.Providers;

namespace API.Plex;

internal class PlexIdConverter(TmdbApi tmdbApi, PlexApi plexApi) : IExternalIdConverter
{
	private const int LookupConcurrency = 4;
	private const int MaxCandidateLookups = 3;

	private static readonly ValueSet<MediaProvider> Guids = [MediaProvider.Plex, MediaProvider.Tmdb, MediaProvider.Imdb, MediaProvider.Tvdb];

	private static readonly ById ByRatingKey = new(Accepts: [MediaProvider.Plex], Yields: Guids, Cost: 1);

	private static readonly ByHints Search = new(Yields: Guids, Cost: 1 + MaxCandidateLookups);

	public MediaProvider Provider => MediaProvider.Plex;

	public IReadOnlyList<IdLookup> Lookups { get; } = [ByRatingKey, Search];

	public IReadOnlyList<IdRoute> Routes { get; } =
	[
		new(Some.Of(MediaProvider.Plex), MediaProvider.Plex, [MediaProvider.Plex], 1),
		new(new None(), MediaProvider.Plex, [MediaProvider.Plex], 1 + MaxCandidateLookups),
		new(Some.Of(MediaProvider.Tmdb), MediaProvider.Plex, [MediaProvider.Tmdb, MediaProvider.Plex], 2 + MaxCandidateLookups),
		new(Some.Of(MediaProvider.Imdb), MediaProvider.Plex, [MediaProvider.Tmdb, MediaProvider.Plex], 2 + MaxCandidateLookups),
	];

	public async Task<Result<IReadOnlyList<IExternalMediaId>>> ConvertAsync(
		IdRoute route,
		IEnumerable<IExternalMediaId> candidates,
		Optional<ResolutionHints> hints,
		CancellationToken cancellationToken = default
	)
	{
		var materialized = candidates.ToList();
		return route.From switch
		{
			Some<MediaProvider>(MediaProvider.Plex) => materialized.OfType<PlexId>().FirstOrDefault() is { Value: not null } plexId
				? await plexApi.LookUpExternalsAsync(plexId, cancellationToken)
				: Error.From("The candidates hold no Plex id to look up."),
			Some<MediaProvider> => await route.ConvertEachAsync(
				materialized,
				source => ResolveByOriginalTitleAsync(source, materialized, cancellationToken),
				"No Plex id could be resolved from an original title."
			),
			None => hints is Some<ResolutionHints>(var hint)
				? await ResolveByTitleAsync(hint.Title, hint.Year, hint.MediaType, materialized, cancellationToken).ToResult("Plex found no title matching the hints.")
				: Error.From("Plex needs title hints to search by."),
		};
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

	private Task<Result<IReadOnlyList<IExternalMediaId>>> ResolveByOriginalTitleAsync(
		IExternalMediaId source,
		List<IExternalMediaId> candidates,
		CancellationToken cancellationToken
	) =>
		GetOriginalTitleAsync(source, cancellationToken)
			.Map(title => ResolveByTitleAsync(title, 0, source.Type, candidates, cancellationToken).ToResult($"Plex found no title matching {title}."));

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

	private async Task<Result<string>> GetOriginalTitleAsync(IExternalMediaId candidate, CancellationToken cancellationToken) =>
		candidate switch
		{
			TmdbId tmdbId => await tmdbApi.GetDetailAsync(tmdbId, cancellationToken).Map(detail => detail.DisplayOriginalTitle),
			ImdbId imdbId => await tmdbApi.FindAsync(imdbId, cancellationToken).Map(item => item.DisplayOriginalTitle),
			_ => Error.From($"TMDB has no original title for a {candidate.Source} id."),
		};

	private static bool ContainsAnyCandidate(IEnumerable<IExternalMediaId> externals, IEnumerable<IExternalMediaId> candidates) =>
		candidates.Any(externals.Contains);
}
