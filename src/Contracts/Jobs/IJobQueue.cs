using Domain.Jobs;

namespace Contracts.Jobs;

public interface IJobQueue
{
	Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default);

	Task<Optional<IJobRequest>> CancelAsync(JobId id, CancellationToken cancellationToken = default);
}
