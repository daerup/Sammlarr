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
	public async IAsyncEnumerable<BlacklistedMediaDetail> GetBlacklistItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		using var activity = SammlarrTelemetry.StartBlacklistLoad(config.WatchlistProvider);
		var entries = await repository.GetBlacklistedEntriesAsync(config.WatchlistProvider.AsMediaProvider(), cancellationToken);

		var pipeline = entries
			.ToAsyncEnumerable()
			.SelectAwait(
				async entry => (entry.AddedAt, await metadataProvider.GetAsync(entry.MediaId, cancellationToken)),
				IMediaMetadataProvider.RecommendedConcurrency,
				cancellationToken
			);

		await foreach (var (addedAt, detail) in pipeline)
		{
			yield return new BlacklistedMediaDetail(detail, addedAt);
		}
	}

	public async Task<IReadOnlySet<IMediaId>> GetBlacklistIdsAsync(CancellationToken cancellationToken = default)
	{
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		var entries = await repository.GetBlacklistedEntriesAsync(config.WatchlistProvider.AsMediaProvider(), cancellationToken);
		return entries.Select(e => e.MediaId).ToHashSet();
	}

	public Task AddToBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default) => repository.AddAsync(mediaId, cancellationToken);

	public Task RemoveFromBacklistAsync(IMediaId mediaId, CancellationToken cancellationToken = default) => repository.RemoveAsync(mediaId, cancellationToken);
}
