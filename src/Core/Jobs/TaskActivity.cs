using System.Collections.Concurrent;
using Contracts.Housekeeping;

namespace Core.Jobs;

internal sealed class TaskActivity(TimeProvider clock)
{
	private readonly ConcurrentDictionary<string, TaskRun> _running = new();

	internal event Action Changed = delegate { };

	internal Optional<TaskRun> Running(string task) => _running.GetValueOrNone(task);

	internal bool TryClaim(string task)
	{
		var claimed = _running.TryAdd(task, new TaskRun(clock.GetUtcNow(), false));
		if (claimed)
		{
			Changed();
		}

		return claimed;
	}

	internal IDisposable Begin(string task)
	{
		_running[task] = new TaskRun(clock.GetUtcNow(), true);
		Changed();
		return new End(this, task);
	}

	internal void Progressed() => Changed();

	internal void Release(string task)
	{
		_running.TryRemove(task, out _);
		Changed();
	}

	private sealed class End(TaskActivity activity, string task) : IDisposable
	{
		public void Dispose() => activity.Release(task);
	}
}
