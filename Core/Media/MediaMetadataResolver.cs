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
		var unanswered = MediaField.None;
		while (Needed(details) is var needed && needed is not MediaField.None)
		{
			var options = Options(details, asked);
			if (EnricherChoice.Next(needed, options, now) is not { } next)
			{
				unanswered |= EnricherChoice.Blocked(needed, options, now);
				break;
			}

			asked.Add(next.Provider);
			switch (await enricherIndex[next.Provider].LookUpAsync(next.Id, cancellationToken))
			{
				case Success<MediaLookup>(var lookup) when lookup.ToIdentified() is Some<IdentifiedMedia>(var enriched):
					details = Merge(details, enriched);
					break;
				case Error { Exception: ProviderUnavailableException }:
					unanswered |= next.Fills;
					break;
			}
		}
		return details is IPartialMedia remaining ? remaining.Confirming(remaining.Missing & ~unanswered) : details;
	}

	private List<EnricherOption> Options(IdentifiedMedia details, IReadOnlySet<MediaProvider> asked)
	{
		var options = new List<EnricherOption>();
		foreach (var provider in Enum.GetValues<EnricherProvider>().Select(enricher => enricher.AsMediaProvider()))
		{
			if (!asked.Contains(provider) && details.Externals.OfProvider(provider) is { } id)
			{
				var fills = enricherIndex[provider].Fills;
				var load = capacity.Of(provider);
				var unavoidable = fills.Overlaps(MediaField.Rating) && OwesRating(details);
				options.Add(new EnricherOption(provider, id, fills, unavoidable ? load with { BlockedUntil = new None() } : load));
			}
		}
		return options;
	}

	private static MediaField Needed(IdentifiedMedia details)
	{
		return (details is IPartialMedia partial ? partial.Missing : MediaField.None) | (OwesRating(details) ? MediaField.Rating : MediaField.None);
	}

	private static IdentifiedMedia Merge(IdentifiedMedia details, IdentifiedMedia enriched) =>
		details switch
		{
			IPartialMedia partial => partial.MergeWith(enriched),
			IMedia => enriched.Rating.Sources.Count > 0 ? MediaMerger.WithRatings(details, [.. enriched.Rating.Sources]) : details,
		};

	private static bool OwesRating(IdentifiedMedia details) => details.Rating.Sources.Count == 0;
}
