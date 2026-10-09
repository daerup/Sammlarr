using Domain.Feed;
using Domain.Feed.Trace;

namespace UI.Features.Feeds.Components;

public interface ILiveRun
{
	Feed Feed { get; }

	RunLayout Layout { get; }

	RunTally Tally { get; }

	bool Hidden { get; }

	Optional<Guid> TraceKey { get; }

	event Action<WireBatch>? Batch;

	event Action? Folded;

	Task<Optional<TraceSnapshot>> SnapshotAsync(CancellationToken cancellationToken = default);

	void Hide();

	void Show();
}
