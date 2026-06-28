using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Settings;

namespace Core.Media;

internal sealed class MediaSearchService(
	IKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider> providers,
	IMediaService mediaService,
	IProviderConfigRepository configRepository
) : IMediaSearchService
{
	public async Task<IReadOnlyList<MediaMatch>> SearchAsync(
		string query,
		IReadOnlyCollection<MediaType> mediaTypes,
		CancellationToken cancellationToken = default
	)
	{
		var config = await configRepository.GetAsync(cancellationToken);
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
