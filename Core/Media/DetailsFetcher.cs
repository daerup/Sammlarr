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
	public async Task<Result<MatchedMedia>> FetchAsync(MediaIdentity identity, MediaWriteBuffer buffer, CancellationToken cancellationToken = default)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		return await LookUpAsync(identity, config.SearchProvider, cancellationToken)
			.Map(found => EnrichAsync(found, config.SearchProvider, buffer, cancellationToken));
	}

	private Task<Result<MatchedMedia>> LookUpAsync(MediaIdentity identity, SearchProvider provider, CancellationToken cancellationToken)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = identity.Externals.OfProvider(providerMedia).GetValueOr(() => identity.Externals.First());
		return searchIndex[provider]
			.LookUpAsync(external, cancellationToken)
			.When(found => found.Fits(identity.Type), "the details could not be fetched")
			.Map(found => found.Matched(identity));
	}

	private async Task<MatchedMedia> EnrichAsync(
		MatchedMedia current,
		SearchProvider searchProvider,
		MediaWriteBuffer buffer,
		CancellationToken cancellationToken
	)
	{
		var now = clock.Now;
		var asked = new HashSet<MediaProvider> { searchProvider.AsMediaProvider() };
		var unconfirmed = MediaField.None;
		while (current.Details.Missing is var missing && missing is not MediaField.None)
		{
			var options = Options(current, missing, asked);
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

	private List<EnricherOption> Options(MatchedMedia current, MediaField missing, IReadOnlySet<MediaProvider> asked)
	{
		return
		[
			.. Enum.GetValues<EnricherProvider>()
				.Select(enricher => enricher.AsMediaProvider())
				.Where(provider => !asked.Contains(provider))
				.Select(provider => current.Identity.Externals.OfProvider(provider).Map(id => Option(provider, id, missing)))
				.WhereSome(),
		];
	}

	private EnricherOption Option(MediaProvider provider, IExternalMediaId id, MediaField missing)
	{
		var fills = enricherIndex[provider].Fills;
		var load = capacity.Of(provider);
		var unavoidable = fills.Overlaps(MediaField.Rating) && missing.Overlaps(MediaField.Rating);
		return new EnricherOption(provider, id, fills, unavoidable ? load with { BlockedUntil = new None() } : load);
	}
}
