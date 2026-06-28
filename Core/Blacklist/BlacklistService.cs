using System.Runtime.CompilerServices;
using Contracts.Blacklist;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Settings;
using Shared;
using Shared.Telemetry;

namespace Core.Blacklist;

internal class BlacklistService(IBlacklistRepository repository, IProviderConfigRepository providerConfigRepository, IMediaService media) : IBlacklistService
{
	public async IAsyncEnumerable<BlacklistedMediaDetail> GetBlacklistItems([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var config = await providerConfigRepository.GetAsync(cancellationToken);
		using var activity = SammlarrTelemetry.StartBlacklistLoad(config.WatchlistProvider);
		var entries = (await repository.GetBlacklistedEntriesAsync(cancellationToken)).ToList();
		var pipeline = media.GetDetailsAsync(entries.Select(e => e.MediaId), cancellationToken).Zip(entries, cancellationToken);

		await foreach (var (result, entry) in pipeline)
		{
			if (result is not MediaMatch detail)
			{
				continue;
			}
			yield return new BlacklistedMediaDetail(detail, entry.AddedAt);
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
