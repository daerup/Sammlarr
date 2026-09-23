using System.Threading.Channels;

namespace Core.Jobs;

internal sealed class JobSignal
{
	private readonly Channel<byte> _nudges = Channel.CreateUnbounded<byte>(new UnboundedChannelOptions { SingleReader = true });

	internal void Raise() => _nudges.Writer.TryWrite(0);

	internal void Complete() => _nudges.Writer.TryComplete();

	internal IAsyncEnumerable<byte> NudgesAsync() => _nudges.Reader.ReadAllAsync();
}
