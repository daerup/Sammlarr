using Contracts.Media.Search;
using Domain.Media;

namespace Integration.Tests.Media;

public sealed class MediaSearchScenarioContext
{
	public IReadOnlyList<MediaLookup>? Results { get; set; }
}
