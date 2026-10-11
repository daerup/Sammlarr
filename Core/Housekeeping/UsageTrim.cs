using Contracts.Housekeeping;
using Contracts.Usage;
using Shared.Options;

namespace Core.Housekeeping;

internal sealed class UsageTrim(IUsageStore usage, HousekeepingOptions options) : IHousekeepingStep
{
	public HousekeepingStep Step => HousekeepingStep.UsageStats;

	public Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
	{
		return usage.PruneAsync(now - TimeSpan.FromDays(options.UsageDays), cancellationToken);
	}
}
