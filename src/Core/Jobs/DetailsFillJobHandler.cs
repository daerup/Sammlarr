using Contracts.Jobs;
using Core.Media;
using Domain.Jobs;
using Domain.Usage;
using Observability.Usage;

namespace Core.Jobs;

internal sealed class DetailsFillJobHandler(DetailsFill fill) : IJobHandler<FillDetails>
{
	public async Task HandleAsync(FillDetails request, CancellationToken cancellationToken = default)
	{
		using var marked = UsageCauses.Mark(UsageCause.DetailsFill);
		await fill.RunAsync(cancellationToken);
	}
}
