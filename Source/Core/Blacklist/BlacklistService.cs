using Contracts;
using Contracts.Media;
using Contracts.Media.Blacklist;
using Contracts.Media.Metadata;
using Contracts.Settings;
using Shared;

namespace Core.Blacklist;

internal class BlacklistService(IBlacklistRepository repository, IProviderConfigRepository providerConfigRepository, IMediaMetadataProvider metadataProvider)
	: IBlacklistService
{
	public async Task<IReadOnlyList<BlacklistedItem>> GetBlacklistItems(CancellationToken cancellationToken = default)
	{
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var entries = await repository.GetBlacklistedEntriesAsync(config.WatchlistProvider.AsMediaProvider(), cancellationToken);
		var addedDateById = entries.ToDictionary(e => e.Id, e => e.AddedDate);
		var ids = entries.Select(e => e.Id).ToList();

		var items = new List<BlacklistedItem>(ids.Count);
		await foreach (var detail in metadataProvider.GetAsync(ids, cancellationToken))
		{
			items.Add(new BlacklistedItem(detail, addedDateById[detail.SourceId]));
		}
		return items;
	}

	public Task AddToBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default) => repository.AddAsync(mediaId, cancellationToken);

	public Task RemoveFromBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default) => repository.RemoveAsync(mediaId, cancellationToken);
}
