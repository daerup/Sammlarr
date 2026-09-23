using Domain.Jobs;

namespace Contracts.Jobs;

public interface IJobQueue
{
	Task<EnqueueResult> EnqueueAsync(IJobRequest request, CancellationToken cancellationToken = default);
}
