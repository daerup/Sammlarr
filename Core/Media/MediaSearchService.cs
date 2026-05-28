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
	public async Task<IReadOnlyList<IMediaDetail>> SearchAsync(
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
		var groups = raws.Select(r => r.Identity.Externals).ToList();
		var identities = await mediaService.ResolveIdentitiesAsync(groups, cancellationToken);
		return raws.Zip(identities, StampIdentity).ToList();
	}

	private static IMediaDetail StampIdentity(IMediaDetail detail, MediaIdentity identity) =>
		detail switch
		{
			MovieDetail m => m with { Identity = identity },
			ShowDetail s => s with { Identity = identity },
			UnresolvedMedia u => u with { Identity = identity },
			_ => detail,
		};
}
