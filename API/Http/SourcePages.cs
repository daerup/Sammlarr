using Observability.Feed;
using Shared.Concurrency;

namespace API.Http;

internal static class SourcePages
{
	public static async Task<T> ReadAsync<T>(int page, Func<Task<T>> read)
	{
		using var _ = LanePriority.Next(LaneClass.Source).Enter();
		return await FeedRunSpans.ReadingAsync(page, read);
	}
}
