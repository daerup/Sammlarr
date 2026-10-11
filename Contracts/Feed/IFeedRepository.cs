using Domain.Feed;
using Domain.Feed.Planning;
using Domain.Feed.Run;
using Domain.Media;

namespace Contracts.Feed;

public interface IFeedRepository
{
	Task<IReadOnlyList<Domain.Feed.Feed>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<Optional<Domain.Feed.Feed>> FindAsync(FeedId id, CancellationToken cancellationToken = default);

	Task<FeedId> CreateAsync(FeedCreateData data, CancellationToken cancellationToken = default);

	Task UpdateAsync(FeedId id, FeedUpdateData data, CancellationToken cancellationToken = default);

	Task DeleteAsync(FeedId id, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedId>> PauseAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedId>> ResumeAsync(IReadOnlyCollection<FeedId> ids, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<MediaId, int>> GetAbsencesAsync(FeedId feedId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<FeedRun>> GetRunsAsync(RunQuery query, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<FeedId, FeedRunTotals>> GetRunTotalsAsync(CancellationToken cancellationToken = default);

	Task StartRunAsync(FeedRun run, CancellationToken cancellationToken = default);

	Task CompleteRunAsync(FeedRun run, Optional<AbsenceChanges> absences, CancellationToken cancellationToken = default);
}
