using Contracts.Identity;
using Domain.Feed;
using Domain.Feed.Run;
using Domain.Media;

namespace Contracts.Feed;

public interface IFeedRunEngine
{
	Task<FeedRunSummary> DryRunAsync(
		Domain.Feed.Feed feed,
		IReadOnlyList<MediaLookup> items,
		Completeness completeness,
		ICurrentUser user,
		CancellationToken cancellationToken = default
	);

	Task<FeedRunResult> PreviewAsync(FeedId feedId, ICurrentUser user, CancellationToken cancellationToken = default);

	Task<FeedRunResult> RunAsync(FeedId feedId, FeedRunTrigger trigger, ICurrentUser user, CancellationToken cancellationToken = default);
}
