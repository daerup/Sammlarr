using System.Runtime.CompilerServices;
using Contracts;
using Contracts.Media;
using Contracts.Media.Blacklist;
using Contracts.Media.Metadata;
using Contracts.Settings;
using Shared;
using Shared.Telemetry;

namespace Core.Blacklist;

internal class BlacklistService(IBlacklistRepository repository, IProviderConfigRepository providerConfigRepository, IMediaMetadataProvider metadataProvider)
	: IBlacklistService
{
	public async IAsyncEnumerable<BlacklistedItem> GetBlacklistItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		using var activity = SammlarrTelemetry.StartBlacklistLoad(config.WatchlistProvider);
		var entries = await repository.GetBlacklistedEntriesAsync(config.WatchlistProvider.AsMediaProvider(), cancellationToken);
		var addedDateById = entries.ToDictionary(e => e.Id, e => e.AddedDate);

		var pipeline = entries
			.ToAsyncEnumerable()
			.SelectAwait(
				async entry => await metadataProvider.GetAsync(entry.Id, cancellationToken),
				IMediaMetadataProvider.RecommendedConcurrency,
				cancellationToken
			);

		await foreach (var detail in pipeline)
		{
			yield return new BlacklistedItem(detail, addedDateById[detail.SourceId]);
		}
	}
    public async Task<IReadOnlySet<IMediaId>> GetBlacklistIdsAsync(CancellationToken cancellationToken = default)
    {
        var config = await providerConfigRepository.GetAsync(cancellationToken);
        var entries = await repository.GetBlacklistedEntriesAsync(config.WatchlistProvider.AsMediaProvider(), cancellationToken);
        return entries.Select(e => e.Id).ToHashSet();
    }

    public Task AddToBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default) => repository.AddAsync(mediaId, cancellationToken);

	public Task RemoveFromBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default) => repository.RemoveAsync(mediaId, cancellationToken);
}
