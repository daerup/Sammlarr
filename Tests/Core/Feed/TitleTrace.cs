using Core.Feed.Trace;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Identity;
using Domain.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Observability.Feed;
using TestInfrastructure;

namespace Core.Tests.Feed;

internal sealed class TitleTrace : IDisposable
{
	private static readonly UserId Owner = UserId.From(Guid.NewGuid());

	private readonly FeedId _feed = FeedId.From(Guid.NewGuid());

	private readonly FeedRunTrace _trace = new(NullLogger<FeedRunTrace>.Instance);

	private readonly IDisposable _run;

	private readonly IDisposable _entered;

	internal TitleTrace()
	{
		_run = _trace.Start(_feed, Owner);
		_entered = FeedRunSpans.Enter(FeedRunSpans.Title("Breaking Bad", new None()));
	}

	internal async Task<NodeTally> NodeAsync(RemoteService service, TraceStage stage)
	{
		return (await SnapshotAsync()).Nodes.Single(node => node.Node == new HopPlace(service, stage));
	}

	internal async Task<IReadOnlyList<NodeTally>> NodesAsync() => (await SnapshotAsync()).Nodes;

	public void Dispose()
	{
		_entered.Dispose();
		_run.Dispose();
		_trace.Dispose();
	}

	private async Task<TraceSnapshot> SnapshotAsync()
	{
		return (await Assert.That(_trace.Read(_feed, Owner, TraceCursor.Start)).IsOfUnionType<TraceRead, TraceSnapshot>())!;
	}
}
