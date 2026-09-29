using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal sealed class MediaSearchService(
	IKeyedServiceIndex<SearchProvider, IMediaSearchProvider> providers,
	IMediaService mediaService,
	IProviderConfigService providerConfig
) : IMediaSearchService
{
	public async Task<IReadOnlyList<MediaLookup>> SearchAsync(
		string query,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		var raws = await providers[config.SearchProvider].SearchAsync(query, mediaTypes, cancellationToken);
		if (raws.Count == 0)
		{
			return raws;
		}
		var groups = raws.Select(r => r.Externals.ToList()).ToList();
		var identities = await mediaService.ResolveIdentitiesAsync(groups, cancellationToken);
		return raws.Zip(identities, (raw, identity) => raw.CombineIdentity(identity)).ToList();
	}
}
