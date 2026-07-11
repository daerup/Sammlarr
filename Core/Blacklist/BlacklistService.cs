using System.Runtime.CompilerServices;
using Contracts.Blacklist;
using Contracts.Media;
using Domain.Blacklist;
using Domain.Media;
using Shared;

namespace Core.Blacklist;

internal sealed class BlacklistService(IBlacklistRepository repository, IMediaService media) : IBlacklistService
{
	public async IAsyncEnumerable<BlacklistedMedia> GetBlacklistItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var entries = (await repository.GetBlacklistedEntriesAsync(cancellationToken)).ToList();
		var pipeline = media.GetDetailsAsync(entries.Select(e => e.MediaId), cancellationToken).ZipAwait(entries, cancellationToken);

		await foreach (var (detail, entry) in pipeline.WhereSuccessAwait())
		{
			yield return new BlacklistedMedia(detail, entry.AddedAt);
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
