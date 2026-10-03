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
	public async Task<IReadOnlyList<MediaMatchResult>> SearchAsync(
		string query,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var config = await providerConfig.GetAsync(cancellationToken);
		var found = await providers[config.SearchProvider].SearchAsync(query, mediaTypes, cancellationToken);
		var identities = await mediaService.RegisterAsync([.. found.Select(item => item.Externals.ToList())], cancellationToken);
		return [.. found.Zip(identities, (item, identity) => item.MatchTo(identity))];
	}
}
