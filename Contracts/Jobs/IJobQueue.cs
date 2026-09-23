using Domain.Jobs;

namespace Contracts.Jobs;

public interface IJobQueue
{
	Task<EnqueueResult> EnqueueAsync(JobRequest request, CancellationToken cancellationToken = default);
}
