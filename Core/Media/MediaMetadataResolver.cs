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
		if (detail is not IdentifiedMedia identified)
		{
			return detail;
		}
		return await EnrichAsync(identified, searchProvider, cancellationToken);
	}

	private Task<Result<MediaLookup>> LookUpAsync(MediaIdentity identity, SearchProvider provider, CancellationToken cancellationToken)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = identity.Externals.OfProvider(providerMedia) ?? identity.Externals.First();
		return searchIndex[provider].LookUpAsync(external, cancellationToken).Map(detail => detail.CombineIdentity(identity));
	}

	private async Task<IdentifiedMedia> EnrichAsync(IdentifiedMedia current, SearchProvider searchProvider, CancellationToken cancellationToken)
	{
		var now = clock.Now;
		var asked = new HashSet<MediaProvider> { searchProvider.AsMediaProvider() };
		var unconfirmed = MediaField.None;
		while (current.Details.Missing is var missing && missing is not MediaField.None)
		{
			var options = Options(current, missing, asked);
			if (EnricherChoice.Next(missing, options, now) is not { } next)
			{
				unconfirmed |= EnricherChoice.BlockedFills(missing, options, now);
				break;
			}

			asked.Add(next.Provider);
			switch (await enricherIndex[next.Provider].LookUpAsync(next.Id, cancellationToken))
			{
				case Success<MediaLookup>(var lookup) when lookup.ToIdentified() is Some<IdentifiedMedia>(var enriched):
					current = Merge(current, enriched);
					break;
				case Error { Exception: ProviderUnavailableException }:
					unconfirmed |= next.Fills;
					break;
			}
		}
		return current with { ConfirmedAbsent = current.Details.Missing.Except(unconfirmed) };
	}

	private static IdentifiedMedia Merge(IdentifiedMedia current, IdentifiedMedia enriched)
	{
		return current with
		{
			Identity = current.Identity with { Externals = current.Identity.Externals + enriched.Identity.Externals },
			Details = current.Details.FillFrom(enriched.Details),
		};
	}

	private List<EnricherOption> Options(IdentifiedMedia current, MediaField missing, IReadOnlySet<MediaProvider> asked)
	{
		var options = new List<EnricherOption>();
		foreach (var provider in Enum.GetValues<EnricherProvider>().Select(enricher => enricher.AsMediaProvider()))
		{
			if (!asked.Contains(provider) && current.Identity.Externals.OfProvider(provider) is { } id)
			{
				var fills = enricherIndex[provider].Fills;
				var load = capacity.Of(provider);
				var unavoidable = fills.Overlaps(MediaField.Rating) && missing.Overlaps(MediaField.Rating);
				options.Add(new EnricherOption(provider, id, fills, unavoidable ? load with { BlockedUntil = new None() } : load));
			}
		}
		return options;
	}
}
