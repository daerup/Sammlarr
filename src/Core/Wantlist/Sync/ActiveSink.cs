using Contracts.DI;
using Contracts.Settings;
using Contracts.Wantlist;
using Domain.Providers;

namespace Core.Wantlist.Sync;

internal sealed class ActiveSink(IProviderConfigService config, IKeyedServiceIndex<SinkKind, IMediaSink> sinks)
{
	public async Task<IMediaSink> ResolveAsync(CancellationToken cancellationToken) => sinks[(await config.GetAsync(cancellationToken)).Sink];

	public IMediaSink Of(SinkKind kind) => sinks[kind];
}
