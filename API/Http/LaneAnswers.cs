using Domain.Providers;
using Shared.Concurrency;

namespace API.Http;

internal static class LaneAnswers
{
	extension<T>(LaneAnswer<T> answer)
		where T : notnull
	{
		public Answer<T> ToAnswer(RemoteService remote)
		{
			return answer switch
			{
				LaneHit<T> hit => new Found<T>(hit.Value),
				LaneMiss => new NoMatch(remote),
				LaneFailed failed => Failed<T>(failed.Cause, remote),
			};
		}
	}

	internal static Answer<T> Failed<T>(Error cause, RemoteService remote)
		where T : notnull
	{
		return cause.Exception.Match<Exception, Answer<T>>(exception => Answering.Thrown<T>(remote, exception, cause), () => new Faulted(remote, cause));
	}
}
