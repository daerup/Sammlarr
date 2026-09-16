using Domain.Identity;
using Domain.Providers;

namespace Contracts.Providers;

public interface IProviderRateLimitReporter
{
	Task ReportAsync(MediaProvider provider, TimeSpan retryAfter, UserId triggeredBy, CancellationToken cancellationToken = default);
}
