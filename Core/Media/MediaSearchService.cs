using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Observability.Usage;

namespace Core.Media;

internal sealed class MediaSearchService(
	IKeyedServiceIndex<SearchProvider, IMediaSearchProvider> providers,
	IMediaService mediaService,
	IMediaRepository mediaRepository,
	IProviderConfigService providerConfig
) : IMediaSearchService
{
	public async Task<IReadOnlyList<MediaMatchResult>> SearchAsync(
		string query,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		using var marked = UsageCauses.Mark(UsageCause.Search);
		var config = await providerConfig.GetAsync(cancellationToken);
		var provider = config.SearchProvider;
		var found = await providers[provider].SearchAsync(query, mediaTypes, cancellationToken);
		var registered = await mediaService.RegisterAsync([.. found.Select(item => item.Externals.ToList())], [.. provider.Service.Remotes], cancellationToken);
		var owners = await OwnersOfAsync(
			[.. found.Zip(registered).Where(hit => hit.Second.IsNone()).Select(hit => OwnKey(provider, hit.First)).WhereSome()],
			cancellationToken
		);
		return
		[
			.. found.Zip(
				registered,
				(item, identity) => identity.Match(claimed => item.MatchTo(claimed), () => Owned(item, OwnKey(provider, item).Map(key => owners.GetValueOrNone(key))))
			),
		];
	}

	private static MediaMatchResult Owned(FoundMedia item, Optional<MediaIdentity> owner)
	{
		return owner.Match<MediaIdentity, MediaMatchResult>(
			found => new MatchedMedia(found, item.Details),
			() => item.Unmatched() with { Reason = UnmatchedReason.Conflict }
		);
	}

	private static Optional<IIdentityKey> OwnKey(SearchProvider provider, FoundMedia item)
	{
		var source = provider switch
		{
			SearchProvider.Tmdb => MediaProvider.Tmdb,
			SearchProvider.Plex => MediaProvider.Plex,
			SearchProvider.Arr => item.Details.Type is MediaType.Show ? MediaProvider.Tvdb : MediaProvider.Tmdb,
		};
		return item.Externals.OfType<IIdentityKey>().FirstOrNone(key => key.Source == source);
	}

	private async Task<IReadOnlyDictionary<IIdentityKey, MediaIdentity>> OwnersOfAsync(IReadOnlyList<IIdentityKey> keys, CancellationToken cancellationToken)
	{
		var stored = await mediaRepository.GetByExternalsAsync(keys, cancellationToken);
		return stored
			.SelectMany(media => media.Identity.Keys.Where(keys.Contains).Select(key => (Key: key, media.Identity)))
			.DistinctBy(owner => owner.Key)
			.ToDictionary(owner => owner.Key, owner => owner.Identity);
	}
}
