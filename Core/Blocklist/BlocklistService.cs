using System.Runtime.CompilerServices;
using Contracts.Blocklist;
using Contracts.Media;
using Domain.Blocklist;
using Domain.Media;

namespace Core.Blocklist;

internal sealed class BlocklistService(IBlocklistRepository repository, IMediaService media) : IBlocklistService
{
	public async IAsyncEnumerable<BlocklistedMedia> GetBlocklistItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var entries = (await repository.GetBlocklistedEntriesAsync(cancellationToken)).ToList();
		var pipeline = media.GetDetailsAsync(entries.Select(e => e.MediaId), cancellationToken).ZipAwait(entries, cancellationToken);

		await foreach (var (detail, entry) in pipeline.WhereSuccessAwait())
		{
			yield return new BlocklistedMedia(detail, entry.AddedAt);
		}
	}

	public async Task<IReadOnlySet<MediaId>> GetBlocklistIdsAsync(CancellationToken cancellationToken = default)
	{
		var entries = await repository.GetBlocklistedEntriesAsync(cancellationToken);
		return entries.Select(e => e.MediaId).ToHashSet();
	}

	public Task AddToBlocklistAsync(MediaId mediaId, CancellationToken cancellationToken = default) => repository.AddAsync(mediaId, cancellationToken);

	public Task RemoveFromBlocklistAsync(MediaId mediaId, CancellationToken cancellationToken = default) => repository.RemoveAsync(mediaId, cancellationToken);
}
