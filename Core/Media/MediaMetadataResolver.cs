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
	IKeyedServiceIndex<RatingProvider, IMediaRatingService> ratingIndex,
	IProviderConfigService providerConfig,
	IProviderCapacity capacity,
	IDateTimeProvider clock
)
{
	public async Task<Result<MediaLookup>> ResolveAsync(MediaIdentity identity, MediaResolutionBuffer buffer, CancellationToken cancellationToken = default)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		return await LookUpAsync(identity, config.SearchProvider, cancellationToken)
			.Map(detail => CompleteAsync(detail, config.SearchProvider, config.RatingProvider, cancellationToken))
			.Apply(buffer.Add);
	}

	private async Task<MediaLookup> CompleteAsync(
		MediaLookup detail,
		SearchProvider searchProvider,
		RatingProvider ratingProvider,
		CancellationToken cancellationToken
	)
	{
		var identified = detail.AsIdentified();
		if (identified is null)
		{
			return detail;
		}
		var enriched = await EnrichAsync(identified.Value, searchProvider, ratingProvider, cancellationToken);
		return await ApplyRatingsAsync(enriched, ratingProvider, cancellationToken);
	}

	private Task<Result<MediaLookup>> LookUpAsync(MediaIdentity identity, SearchProvider provider, CancellationToken cancellationToken)
	{
		var providerMedia = provider.AsMediaProvider();
		var external = identity.Externals.OfProvider(providerMedia) ?? identity.Externals.First();
		return searchIndex[provider].LookUpAsync(external, cancellationToken).Map(detail => detail.CombineIdentity(identity));
	}

	private async Task<IdentifiedMedia> ApplyRatingsAsync(IdentifiedMedia details, RatingProvider ratingProvider, CancellationToken cancellationToken)
	{
		if (HasRatingFrom(details.Rating, ratingProvider))
		{
			return details;
		}

		var ratingId = details.Externals.OfProvider(ratingProvider.AsMediaProvider());
		if (ratingId is null)
		{
			return details;
		}

		return await ratingIndex[ratingProvider]
			.GetRatingsAsync(ratingId, cancellationToken)
			.Map(ratings => MediaMerger.WithRatings(details, ratings))
			.GetValueOr(details);
	}

	private async Task<IdentifiedMedia> EnrichAsync(
		IdentifiedMedia details,
		SearchProvider searchProvider,
		RatingProvider ratingProvider,
		CancellationToken cancellationToken
	)
	{
		var now = clock.Now;
		var asked = new HashSet<MediaProvider> { searchProvider.AsMediaProvider() };
		var unanswered = MediaField.None;
		while (details is IPartialMedia partial)
		{
			var needed = Needed(partial, ratingProvider);
			var options = Options(partial, ratingProvider, asked);
			if (EnricherChoice.Next(needed, options, now) is not { } next)
			{
				unanswered |= EnricherChoice.Blocked(needed, options, now);
				break;
			}

			asked.Add(next.Provider);
			switch (await enricherIndex[next.Provider].LookUpAsync(next.Id, cancellationToken))
			{
				case Success<MediaLookup>(var lookup) when lookup.ToIdentified() is Some<IdentifiedMedia>(var enriched):
					details = partial.MergeWith(enriched);
					break;
				case Error { Exception: ProviderUnavailableException }:
					unanswered |= next.Fills;
					break;
			}
		}
		return details is IPartialMedia remaining ? remaining.Confirming(remaining.Missing & ~unanswered) : details;
	}

	private List<EnricherOption> Options(IPartialMedia partial, RatingProvider ratingProvider, IReadOnlySet<MediaProvider> asked)
	{
		var options = new List<EnricherOption>();
		foreach (var provider in Enum.GetValues<EnricherProvider>().Select(enricher => enricher.AsMediaProvider()))
		{
			if (!asked.Contains(provider) && partial.Identity.Externals.OfProvider(provider) is { } id)
			{
				var fills = enricherIndex[provider].Fills;
				var ratesForUs = provider == ratingProvider.AsMediaProvider();
				var load = capacity.Of(provider);
				var unavoidable = ratesForUs && !HasRatingFrom(partial.Rating, ratingProvider);
				options.Add(
					new EnricherOption(provider, id, ratesForUs ? fills : fills & ~MediaField.Rating, unavoidable ? load with { BlockedUntil = new None() } : load)
				);
			}
		}
		return options;
	}

	private static MediaField Needed(IPartialMedia partial, RatingProvider ratingProvider) =>
		HasRatingFrom(partial.Rating, ratingProvider) ? partial.Missing : partial.Missing | MediaField.Rating;

	private static bool HasRatingFrom(RatingSummary rating, RatingProvider provider) => rating.Sources.Any(source => source.Source == provider);
}
