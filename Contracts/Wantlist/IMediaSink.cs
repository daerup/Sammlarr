using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist.Outbox;
using Domain.Wantlist.Sync;

namespace Contracts.Wantlist;

public interface IMediaSink
{
	SinkKind Kind { get; }

	Optional<MediaProvider> AddressedBy(MediaType type);

	Task<SinkSnapshot> GetAsync(CancellationToken cancellationToken = default);

	IAsyncEnumerable<SinkSyncAck> ApplyAsync(WantlistOperationAction action, IReadOnlyList<SinkPush> pushes, CancellationToken cancellationToken = default);

	Task<Result<SinkHoldings>> HoldingsOfAsync(User person, CancellationToken cancellationToken = default);

	Task<Result<int>> DeleteTitlesOfAsync(User person, CancellationToken cancellationToken = default);
}

public readonly record struct SinkHoldings(int Sole, int Shared, string Tag);

public readonly record struct SinkPush(MediaId MediaId, Optional<Domain.Feed.Feed> AddedBy);

public readonly record struct SinkSyncAck(MediaId MediaId, SinkSyncOutcome Outcome);

public enum SinkSyncOutcome
{
	Applied,
	Failed,
	Unaddressable,
}
