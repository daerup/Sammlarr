using Contracts.Auth;
using Contracts.Usage;
using Domain.Providers;
using Domain.Usage;

namespace API.Mdblist;

internal sealed class MdblistRequestsLeft(IApiKeyConfigRepository keys, MdblistBudget budget) : IMdblistRequestsLeft
{
	public async Task<Optional<QuotaToday>> TodayAsync(CancellationToken cancellationToken = default)
	{
		var key = await keys.GetAsync(ApiKeyProvider.Mdblist, cancellationToken);
		return key.ToOptional().Map(budget.Today);
	}
}
