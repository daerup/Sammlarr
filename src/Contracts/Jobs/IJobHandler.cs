using Domain.Jobs;

namespace Contracts.Jobs;

public interface IJobHandler<in TRequest>
	where TRequest : IJobRequest
{
	Task HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
