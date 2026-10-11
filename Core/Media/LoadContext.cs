using Core.Wantlist.Sync;
using Domain.Media;
using Domain.Providers;

namespace Core.Media;

internal sealed record LoadContext(Func<MediaType, Optional<MediaProvider>> AddressedBy, IReadOnlySet<RemoteService> IdServices, DetailsRun Details)
{
	internal static async Task<LoadContext> StartAsync(
		ActiveSink sink,
		MediaIdResolver ids,
		DetailsFetcher details,
		FieldNeeds needs,
		LibraryWriter writer,
		CancellationToken cancellationToken
	)
	{
		var active = await sink.ResolveAsync(cancellationToken);
		var run = new DetailsRun(await details.ConnectedAsync(cancellationToken), needs, writer);
		return new LoadContext(active.AddressedBy, await ids.ConnectedAsync(cancellationToken), run);
	}
}
