using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;

namespace Contracts.Feed;

public interface IFeedRunLog
{
	Task<IReadOnlyDictionary<MediaId, int>> GetAbsencesAsync(
		FeedId feedId,
		IReadOnlySet<MediaId> owned,
		int window,
		CancellationToken cancellationToken = default
	);

	Task<IReadOnlyList<FeedRun>> GetRunsAsync(FeedId feedId, int take, CancellationToken cancellationToken = default);

	Task<Optional<FeedRun>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);

	Task<FeedRunHistory> GetHistoryAsync(FeedId feedId, CancellationToken cancellationToken = default);

	Task RecordAsync(FeedRunRecord record, CancellationToken cancellationToken = default);
}
