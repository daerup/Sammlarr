using Contracts.Media;
using Contracts.Media.Search;

namespace Integration.Tests.Blacklist;

public sealed class BlacklistScenarioContext
{
	public List<MediaMatch>? Items { get; set; }
	public IReadOnlySet<MediaId>? Ids { get; set; }
}
