using Contracts;

namespace Integration.Tests.Browse;

public sealed class TmdbTrendingScenarioContext
{
	public PagedResult<MediaMatch>? TrendingPage { get; set; }
}
