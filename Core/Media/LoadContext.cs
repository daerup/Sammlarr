using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal sealed record LoadContext(
	Func<MediaType, Optional<MediaProvider>> AddressedBy,
	IReadOnlySet<RemoteService> IdServices,
	IReadOnlySet<RemoteService> DetailServices,
	MediaWriteBuffer Writes
)
{
	internal static async Task<LoadContext> StartAsync(
		ActiveSink sink,
		MediaIdResolver ids,
		DetailsFetcher details,
		MediaWriteBuffer writes,
		CancellationToken cancellationToken
	)
	{
		var active = await sink.ResolveAsync(cancellationToken);
		return new LoadContext(active.AddressedBy, await ids.ConnectedAsync(cancellationToken), await details.ConnectedAsync(cancellationToken), writes);
	}
}
