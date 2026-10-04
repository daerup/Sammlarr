using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Providers;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal sealed class DetailsFetcher(
	IKeyedServiceIndex<SearchProvider, IMediaSearchProvider> searchIndex,
	IKeyedServiceIndex<MediaProvider, IDetailsEnricher> enricherIndex,
	IProviderConfigService providerConfig,
	IProviderCapacity capacity,
	IDateTimeProvider clock
)
{
	private static readonly DetailsSource TmdbDetails = new(SearchProvider.Tmdb, [MediaProvider.Tmdb, MediaProvider.Tvdb, MediaProvider.Imdb]);

	private static readonly DetailsSource PlexDetails = new(SearchProvider.Plex, [MediaProvider.Plex]);

	public async Task<Result<MatchedMedia>> FetchAsync(MediaIdentity identity, MediaWriteBuffer buffer, CancellationToken cancellationToken = default)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		var primary = Primary(identity, config.SearchProvider);
		return await LookUpAsync(identity, primary, cancellationToken).Map(found => EnrichAsync(found, primary, buffer, cancellationToken));
	}

	private static DetailsSource Primary(MediaIdentity identity, SearchProvider search)
	{
		DetailsSource[] order = search switch
		{
			SearchProvider.Plex => [PlexDetails, TmdbDetails],
			SearchProvider.Tmdb or SearchProvider.Arr => [TmdbDetails, PlexDetails],
		};
		return order.FirstOrNone(source => source.IdOf(identity).IsSome()).GetValueOr(order[0]);
	}

	private Task<Result<MatchedMedia>> LookUpAsync(MediaIdentity identity, DetailsSource primary, CancellationToken cancellationToken)
	{
		var external = primary.IdOf(identity).GetValueOr(() => identity.Externals.First());
		return searchIndex[primary.Search]
			.LookUpAsync(external, cancellationToken)
			.When(found => found.Fits(identity.Type), "the details could not be fetched")
			.Map(found => found.Matched(identity));
	}

	private async Task<MatchedMedia> EnrichAsync(MatchedMedia current, DetailsSource primary, MediaWriteBuffer buffer, CancellationToken cancellationToken)
	{
		var now = clock.Now;
		var asked = new HashSet<MediaProvider> { primary.Reads[0] };
		var unconfirmed = MediaField.None;
		while (current.Details.Missing is var missing && missing is not MediaField.None)
		{
			var options = Options(current, asked);
			if (EnricherChoice.Next(missing, options, now) is not Some<EnricherOption>(var next))
			{
				unconfirmed |= EnricherChoice.BlockedFills(missing, options, now);
				break;
			}

			asked.Add(next.Provider);
			var answer = await enricherIndex[next.Provider].LookUpAsync(next.Id, cancellationToken);
			answer.ToOptional().When(found => found.Fits(current.Details.Type)).Apply(found => current = Merge(current, found));
			unconfirmed |= answer.Match(_ => MediaField.None, error => error.CausedBy<ProviderUnavailableException>().Match(next.Fills, MediaField.None));
		}
		buffer.Add(current, new CachedDetails(current.Details, now, current.Details.Missing.Except(unconfirmed)));
		return current;
	}

	private static MatchedMedia Merge(MatchedMedia current, FoundMedia found)
	{
		return current with
		{
			Identity = current.Identity with { Externals = current.Identity.Externals + found.Externals },
			Details = current.Details.FillFrom(found.Details),
		};
	}

	private List<EnricherOption> Options(MatchedMedia current, IReadOnlySet<MediaProvider> asked)
	{
		return
		[
			.. Enum.GetValues<EnricherProvider>()
				.Select(enricher => enricher.AsMediaProvider())
				.Where(provider => !asked.Contains(provider))
				.Select(provider => current.Identity.Externals.OfProvider(provider).Map(id => Option(provider, id, current.Details.Rating)))
				.WhereSome(),
		];
	}

	private EnricherOption Option(MediaProvider provider, IExternalMediaId id, RatingSummary ratings)
	{
		var fills = enricherIndex[provider].Fills;
		var load = capacity.Of(provider);
		var unavoidable = provider == MediaProvider.Tmdb && fills.Overlaps(MediaField.Ratings) && ratings.Sources.Count == 0;
		return new EnricherOption(provider, id, fills, unavoidable ? load with { BlockedUntil = new None() } : load);
	}

	private sealed record DetailsSource(SearchProvider Search, IReadOnlyList<MediaProvider> Reads)
	{
		public Optional<IExternalMediaId> IdOf(MediaIdentity identity)
		{
			return Reads.Select(provider => identity.Externals.OfProvider(provider)).WhereSome().FirstOrNone(_ => true);
		}
	}
}
