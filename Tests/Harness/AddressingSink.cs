using Contracts.Wantlist;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;

namespace Harness;

internal sealed class AddressingSink(IMediaSink sink, Func<MediaType, Optional<MediaProvider>> addressing) : IMediaSink
{
	public SinkKind Kind => sink.Kind;

	public Optional<MediaProvider> AddressedBy(MediaType type) => addressing(type);

	public Task<SinkSnapshot> GetAsync(CancellationToken cancellationToken = default) => sink.GetAsync(cancellationToken);

	public IAsyncEnumerable<SinkSyncAck> ApplyAsync(WantlistOperationAction action, IReadOnlyList<SinkPush> pushes, CancellationToken cancellationToken = default)
	{
		return sink.ApplyAsync(action, pushes, cancellationToken);
	}
}
