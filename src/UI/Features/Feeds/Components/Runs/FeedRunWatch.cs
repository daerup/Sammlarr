using System.Runtime.CompilerServices;
using Contracts.Feed;
using Contracts.Providers;
using Domain.Feed;
using Domain.Feed.Trace;

namespace UI.Features.Feeds.Components.Runs;

internal sealed class FeedRunWatch(
	Feed feed,
	RunLayout layout,
	Optional<int> previous,
	IFeedRunTraces traces,
	IRemoteCapacity capacity,
	TimeProvider clock,
	StrongBox<bool> hidden
) : ILiveRun, IDisposable
{
	private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(250);

	private static readonly TimeSpan Linger = TimeSpan.FromSeconds(1.5);

	private readonly PeriodicTimer _timer = new(Tick);

	private readonly Lock _gate = new();

	private TraceCursor _cursor = TraceCursor.Start;

	private Optional<DateTimeOffset> _endedAt = new None();

	private bool _traced;

	private volatile bool _settled;

	private volatile bool _folded;

	public Feed Feed => feed;

	public RunLayout Layout => layout;

	public RunTally Tally { get; private set; } = RunTally.Before(previous);

	public bool Hidden => hidden.Value;

	public bool HasFolded => _folded;

	public Optional<Guid> TraceKey
	{
		get
		{
			lock (_gate)
			{
				return _cursor.Run;
			}
		}
	}

	public event Action<WireBatch>? Batch;

	public event Action? Folded;

	internal event Action? Changed;

	public async Task<Optional<TraceSnapshot>> SnapshotAsync(CancellationToken cancellationToken = default)
	{
		var read = await traces.ReadAsync(feed.Id, TraceCursor.Start, cancellationToken);
		return read switch
		{
			TraceSnapshot snapshot => Some.Of(snapshot),
			TraceBatch => new None(),
			NoTrace => new None(),
		};
	}

	public void Hide()
	{
		hidden.Value = true;
		Changed?.Invoke();
	}

	public void Show()
	{
		hidden.Value = false;
		Changed?.Invoke();
	}

	internal void Settle() => _settled = true;

	public void Dispose() => _timer.Dispose();

	internal async Task WatchAsync()
	{
		do
		{
			if (await ReadAsync())
			{
				Dispose();
				_folded = true;
				Folded?.Invoke();
				return;
			}
		} while (await _timer.WaitForNextTickAsync());
	}

	private async Task<bool> ReadAsync()
	{
		var read = await traces.ReadAsync(feed.Id, _cursor);
		return read switch
		{
			TraceSnapshot snapshot => Took(snapshot.Cursor, snapshot.ElapsedMs, [], snapshot.Fates, snapshot.Requests, snapshot.Ended, snapshot.Progress),
			TraceBatch batch => Took(batch.Cursor, batch.ElapsedMs, batch.Steps, batch.Fates, batch.Requests, batch.Ended, batch.Progress),
			NoTrace => _traced || _settled,
		};
	}

	private bool Took(
		TraceCursor cursor,
		int elapsedMs,
		IReadOnlyList<TraceStep> steps,
		FateTally fates,
		Optional<int> requests,
		Optional<FeedRunEnd> ended,
		RunProgress progress
	)
	{
		var now = clock.GetUtcNow();
		_traced = true;
		lock (_gate)
		{
			_cursor = cursor;
		}

		_endedAt = ended.Map(_ => _endedAt.GetValueOr(now));
		Tally = RunTally.Of(fates, requests, elapsedMs, ended, previous) with
		{
			Progress = progress,
			RateLimited = progress.Busiest.Match(wait => !capacity.Of(wait.Service).IsOpen(now), () => false),
		};
		IReadOnlyList<WireCapacity> load =
		[
			.. layout
				.Ids.Select(node => node.Service)
				.Concat(layout.Details)
				.Distinct()
				.Select(service => LiveWire.Capacity(service, capacity.Of(service), now, elapsedMs)),
		];
		Batch?.Invoke(new WireBatch([.. steps.Select(LiveWire.Step)], load, ended.IsSome()) { Reading = progress.Reading.ToNullable() });
		Changed?.Invoke();
		return _endedAt.Match(at => now - at >= Linger, () => false);
	}
}
