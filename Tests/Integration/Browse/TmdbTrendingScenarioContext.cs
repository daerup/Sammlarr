using Contracts;
using Domain.Media;

namespace Integration.Tests.Browse;

public sealed class TmdbTrendingScenarioContext
{
	public PagedResult<MediaLookup>? TrendingPage { get; set; }
}
