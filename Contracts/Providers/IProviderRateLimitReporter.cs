using Domain.Identity;
using Domain.Providers;

namespace Contracts.Providers;

public interface IProviderRateLimitReporter
{
	Task ReportAsync(RemoteService remote, TimeSpan retryAfter, UserId triggeredBy, CancellationToken cancellationToken = default);
}
