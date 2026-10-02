using Contracts;
using Contracts.DI;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Providers;
using Contracts.Settings;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;

namespace Core.Media;

internal sealed class MediaMetadataResolver(
	IKeyedServiceIndex<SearchProvider, IMediaSearchProvider> searchIndex,
	IKeyedServiceIndex<MediaProvider, IMetadataEnricher> enricherIndex,
	IProviderConfigService providerConfig,
	IProviderCapacity capacity,
	IDateTimeProvider clock
)
{
	public async Task<Result<MediaLookup>> ResolveAsync(MediaIdentity identity, MediaResolutionBuffer buffer, CancellationToken cancellationToken = default)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		return await LookUpAsync(identity, config.SearchProvider, cancellationToken)
			.Map(detail => CompleteAsync(detail, config.SearchProvider, cancellationToken))
			.Apply(buffer.Add);
	}

	private async Task<MediaLookup> CompleteAsync(MediaLookup detail, SearchProvider searchProvider, CancellationToken cancellationToken)
	{
		var identified = detail.AsIdentified();
		if (identified is null)
		{
			return detail;
		}
		return await EnrichAsync(identified.Value, searchProvider, cancellationToken);
	}

	private Task<Result<MediaLookup>> LookUpAsync(MediaIdentity identity, SearchProvider provider, CancellationToken cancellationToken)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = identity.Externals.OfProvider(providerMedia) ?? identity.Externals.First();
		return searchIndex[provider].LookUpAsync(external, cancellationToken).Map(detail => detail.CombineIdentity(identity));
	}

	private async Task<IdentifiedMedia> EnrichAsync(IdentifiedMedia details, SearchProvider searchProvider, CancellationToken cancellationToken)
	{
		var now = clock.Now;
		var asked = new HashSet<MediaProvider> { searchProvider.AsMediaProvider() };
		var unconfirmed = MediaField.None;
		while (details is IPartialMedia partial)
		{
			var options = Options(partial, asked);
			if (EnricherChoice.Next(partial.Missing, options, now) is not { } next)
			{
				unconfirmed |= EnricherChoice.BlockedFills(partial.Missing, options, now);
				break;
			}

			asked.Add(next.Provider);
			switch (await enricherIndex[next.Provider].LookUpAsync(next.Id, cancellationToken))
			{
				case Success<MediaLookup>(var lookup) when lookup.ToIdentified() is Some<IdentifiedMedia>(var enriched):
					details = partial.MergeWith(enriched);
					break;
				case Error { Exception: ProviderUnavailableException }:
					unconfirmed |= next.Fills;
					break;
			}
		}
		return details is IPartialMedia remaining ? remaining.WithConfirmedAbsent(remaining.Missing.Except(unconfirmed)) : details;
	}

	private List<EnricherOption> Options(IPartialMedia partial, IReadOnlySet<MediaProvider> asked)
	{
		var options = new List<EnricherOption>();
		foreach (var provider in Enum.GetValues<EnricherProvider>().Select(enricher => enricher.AsMediaProvider()))
		{
			if (!asked.Contains(provider) && partial.Identity.Externals.OfProvider(provider) is { } id)
			{
				var fills = enricherIndex[provider].Fills;
				var load = capacity.Of(provider);
				var unavoidable = fills.Overlaps(MediaField.Rating) && partial.Missing.Overlaps(MediaField.Rating);
				options.Add(new EnricherOption(provider, id, fills, unavoidable ? load with { BlockedUntil = new None() } : load));
			}
		}
		return options;
	}
}
