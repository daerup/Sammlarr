using Contracts;
using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Settings;
using Core.Media.Metadata;
using Shared.Telemetry;

namespace Core.Media;

internal sealed class MediaMetadataResolver(
	IKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider> searchIndex,
	IKeyedServiceIndex<MediaProvider, IMetadataEnricher> enricherIndex,
	IKeyedServiceIndex<RatingProvider, IMediaRatingService> ratingIndex,
	IProviderConfigRepository configRepository
)
{
	private static readonly MediaProvider[] EnricherChain = [MediaProvider.Plex, MediaProvider.Tmdb];

	public async Task<Result<IMediaDetail>> ResolveAsync(MediaIdentity identity, MediaResolutionBuffer buffer, CancellationToken cancellationToken = default)
	{
		using var activity = SammlarrTelemetry.StartMetadataItem(identity.Id);

		var config = await configRepository.GetAsync(cancellationToken);
		var detail = await LookUpAsync(identity, config.SearchProvider, cancellationToken);
		if (detail is not UnresolvedMedia)
		{
			detail = await EnrichAsync(detail, config.SearchProvider, cancellationToken);
			detail = await ApplyRatingsAsync(detail, config.RatingProvider, cancellationToken);
		}

		var known = identity.Externals.ToHashSet();
		var discovered = detail.Identity.Externals.Where(e => !known.Contains(e)).ToList();

		var extendedIdentity = identity with { Externals = [.. identity.Externals, .. discovered] };
		IMediaDetail stamped = detail switch
		{
			MovieDetail m => m with { Identity = extendedIdentity },
			ShowDetail s => s with { Identity = extendedIdentity },
			UnresolvedMedia u => u with { Identity = extendedIdentity },
		};
		SammlarrTelemetry.TagItemSuccess(activity, stamped is not UnresolvedMedia);
		buffer.Add(stamped);
		return stamped;
	}

	private async Task<IMediaDetail> LookUpAsync(MediaIdentity identity, MediaSearchProvider provider, CancellationToken cancellationToken)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = identity.Externals.FirstOrDefault(e => e.Source == providerMedia) ?? identity.Externals.First();
		using var activity = SammlarrTelemetry.StartMetadataLookup(provider, external);
		var detail = await searchIndex[provider].LookUpAsync(external, cancellationToken);
		var found = detail.Identity.Externals.ToHashSet();
		var preserved = identity.Externals.Where(e => !found.Contains(e)).ToList();
		return detail switch
		{
			MovieDetail m => m with { Identity = identity with { Externals = [.. m.Identity.Externals, .. preserved] } },
			ShowDetail s => s with { Identity = identity with { Externals = [.. s.Identity.Externals, .. preserved] } },
			UnresolvedMedia u => u with { Identity = identity with { Externals = [.. u.Identity.Externals, .. preserved] } },
		};
	}

	private async Task<IMediaDetail> ApplyRatingsAsync(IMediaDetail details, RatingProvider ratingProvider, CancellationToken cancellationToken)
	{
		var ratingId = FindId(details, ratingProvider.AsMediaProvider());
		if (ratingId is null)
		{
			return details;
		}

		using var activity = SammlarrTelemetry.StartMetadataRatings(ratingProvider);
		var ratings = await ratingIndex[ratingProvider].GetRatingsAsync(ratingId, cancellationToken);
		return ratings.Count == 0 ? details : MetadataMerger.WithRatings(details, ratings);
	}

	private async Task<IMediaDetail> EnrichAsync(IMediaDetail details, MediaSearchProvider searchProvider, CancellationToken cancellationToken)
	{
		var primaryProvider = searchProvider.AsMediaProvider();
		foreach (var provider in EnricherChain.Where(p => p != primaryProvider))
		{
			var missing = MetadataCompleteness.GetMissingFields(details);
			if (missing.Count == 0)
			{
				break;
			}

			var providerId = FindId(details, provider);
			if (providerId is null)
			{
				continue;
			}

			using var activity = SammlarrTelemetry.StartMetadataEnrich(providerId, missing);
			var enrichment = await enricherIndex[provider].LookUpAsync(providerId, cancellationToken);
			if (enrichment is not null)
			{
				details = MetadataMerger.FillMissing(details, enrichment);
			}
		}
		return details;
	}

	private static IExternalMediaId? FindId(IMediaDetail details, MediaProvider provider) =>
		details.Identity.Externals.FirstOrDefault(id => id.Source == provider);
}
