using Contracts.Settings;
using Contracts.Wantlist;
using Core.Wantlist.Sync;
using Domain.Providers;
using TestInfrastructure;

namespace Core.Tests.Media;

internal static class TestSinks
{
	internal static ActiveSink AddressingBy(MediaProvider provider, IProviderConfigService config)
	{
		var sink = IMediaSink.Mock();
		sink.AddressedBy(Any()).Returns(Some.Of(provider));
		return new ActiveSink(config, new FakeKeyedServiceIndex<SinkKind, IMediaSink>().Register(SinkKind.Plex, sink.Object));
	}
}
