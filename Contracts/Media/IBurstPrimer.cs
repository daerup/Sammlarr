using Domain.Media;
using Domain.Providers;

namespace Contracts.Media;

public interface IBurstPrimer
{
	RemoteService Remote { get; }

	Task PrimeAsync(IReadOnlyCollection<IReadOnlyList<IExternalMediaId>> titles, CancellationToken cancellationToken);
}
