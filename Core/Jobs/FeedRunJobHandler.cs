using Contracts.Feed;
using Contracts.Jobs;
using Domain.Jobs;

namespace Core.Jobs;

internal sealed class FeedRunJobHandler(IFeedRunEngine engine) : IJobHandler<RunFeed>
{
	public async Task HandleAsync(RunFeed request, CancellationToken cancellationToken = default) =>
		await engine.RunAsync(request.Id, request.Trigger, cancellationToken);
}
