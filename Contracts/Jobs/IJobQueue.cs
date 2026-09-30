using Domain.Jobs;

namespace Contracts.Jobs;

public interface IJobQueue
{
	Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default);

	Task<bool> CancelAsync(JobId id, CancellationToken cancellationToken = default);
}
