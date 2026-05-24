using System.Runtime.CompilerServices;
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
		var entries = await repository.GetBlacklistedEntriesAsync(cancellationToken);

		var pipeline = entries
			.ToAsyncEnumerable()
			.SelectAwait(
				async entry => (entry.AddedAt, Detail: await metadataProvider.GetAsync(entry.MediaId, cancellationToken)),
				IMediaMetadataProvider.RecommendedConcurrency,
				cancellationToken
			);

		await foreach (var (addedAt, detail) in pipeline)
		{
			if (detail is null)
			{
				continue;
			}
			yield return new BlacklistedMediaDetail(detail, addedAt);
		}
	}

	public async Task<IReadOnlySet<MediaId>> GetBlacklistIdsAsync(CancellationToken cancellationToken = default)
	{
		var entries = await repository.GetBlacklistedEntriesAsync(cancellationToken);
		return entries.Select(e => e.MediaId).ToHashSet();
	}

	public Task AddToBacklistAsync(MediaId mediaId, CancellationToken cancellationToken = default) => repository.AddAsync(mediaId, cancellationToken);

	public Task RemoveFromBacklistAsync(MediaId mediaId, CancellationToken cancellationToken = default) => repository.RemoveAsync(mediaId, cancellationToken);
}
