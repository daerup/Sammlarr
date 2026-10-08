using Contracts.Auth;
using Contracts.Usage;
using Domain.Providers;

namespace API.Mdblist;

internal sealed class MdblistRequestsLeft(IApiKeyConfigRepository keys, MdblistBudget budget) : IMdblistRequestsLeft
{
	public async Task<Optional<long>> TodayAsync(CancellationToken cancellationToken = default)
	{
		var key = await keys.GetAsync(ApiKeyProvider.Mdblist, cancellationToken);
		return key.ToOptional().Map(found => budget.Left(found));
	}
}
