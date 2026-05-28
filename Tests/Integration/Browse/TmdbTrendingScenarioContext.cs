using Contracts.Media.Trending;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Browse;

public sealed class TmdbTrendingScenarioContext
{
	public List<RegisteredMedia> TrendingItems { get; } = [];
	public TrendingPageResult? Result { get; set; }
}
