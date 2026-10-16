using Domain.Jobs;

namespace UI.Features.Feeds.Components;

internal static class FeedJobStates
{
	extension(Optional<JobState> job)
	{
		internal bool IsRunning => job.Match(state => state is JobState.Running, () => false);

		internal bool IsQueued => job.Match(state => state is JobState.Waiting or JobState.Queued, () => false);
	}
}
