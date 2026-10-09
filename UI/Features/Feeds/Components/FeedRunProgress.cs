using Contracts;
using Contracts.Announcing;
using Contracts.Feed;
using Contracts.Identity;
using Contracts.Jobs;
using Contracts.Providers;
using Domain.Announcing;
using Domain.Feed;
using Domain.Feed.Trace;
using Domain.Identity;
using Domain.Jobs;
using UI.Shared.Announcing;

namespace UI.Features.Feeds.Components;

internal sealed class FeedRunProgress(
	IJobQueueView queue,
	IEventPublisher events,
	IFeedService feeds,
	IFeedRunLayout layouts,
	IFeedRunTraces traces,
	IRemoteCapacity capacity,
	ICurrentUser currentUser,
	IDateTimeProvider clock
) : IDisposable
{
	private readonly Lock _gate = new();

	private FeedRunWatch? _watch;

	private Request? _request;

	private UserId? _viewer;

	private bool _disposed;

	public event Action? Changed;

	public Optional<ILiveRun> Live => Optional.Of<ILiveRun>(_watch);

	public async Task StartAsync(CancellationToken cancellationToken = default)
	{
		if (_viewer is not null || !currentUser.IsAuthenticated)
		{
			return;
		}

		_viewer = currentUser.Id;
		events.Published += OnEvent;
		var running = (await queue.GetAsync(cancellationToken)).Jobs.Where(job => job.State is JobState.Running).Select(job => job.Request);
		await running.OfType<RunFeed>().FirstOrNone().Match(run => BeginAsync(Ask(run.Id)), () => Task.CompletedTask);
	}

	public void Dispose()
	{
		events.Published -= OnEvent;
		lock (_gate)
		{
			_disposed = true;
			_watch?.Stop();
		}
	}

	private void OnEvent(IEvent raised)
	{
		if (ViewerEvent.Own<JobStateChangedEvent>(raised, _viewer) is not { Kind: JobKind.FeedRun } job)
		{
			return;
		}

		var feed = FeedId.From(Guid.Parse(job.Key));
		if (job.State is JobState.Running)
		{
			_ = BeginAsync(Ask(feed));
		}
		else if (job.State is JobState.Done or JobState.Failed or JobState.Cancelled)
		{
			Settle(feed);
		}
	}

	private Request Ask(FeedId feed)
	{
		lock (_gate)
		{
			_request = new Request(feed);
			return _request;
		}
	}

	private void Settle(FeedId feed)
	{
		lock (_gate)
		{
			if (_request is { } request && request.Feed == feed)
			{
				request.Settled = true;
			}

			if (_watch is { } watch && watch.Feed.Id == feed)
			{
				watch.Settle();
			}
		}
	}

	private async Task BeginAsync(Request request)
	{
		var found = (await feeds.GetAsync()).Find(request.Feed);
		var layout = await layouts.GetAsync(request.Feed);
		found.Apply(overview => Watch(request, overview, layout));
	}

	private void Watch(Request request, FeedOverview overview, RunLayout layout)
	{
		FeedRunWatch watch;
		lock (_gate)
		{
			if (_disposed || _request != request)
			{
				return;
			}

			watch = new FeedRunWatch(overview.Feed, layout, overview.LastRun.Map(run => run.Counts.Total), traces, capacity, clock);
			if (request.Settled)
			{
				watch.Settle();
			}

			watch.Changed += OnChanged;
			watch.Folded += () => Fold(watch);
			_watch?.Stop();
			_watch = watch;
		}

		Changed?.Invoke();
		_ = watch.WatchAsync();
	}

	private void Fold(FeedRunWatch watch)
	{
		lock (_gate)
		{
			if (_watch != watch)
			{
				return;
			}

			_watch = null;
		}

		Changed?.Invoke();
	}

	private void OnChanged() => Changed?.Invoke();

	private sealed class Request(FeedId feed)
	{
		internal FeedId Feed => feed;

		internal bool Settled { get; set; }
	}
}
