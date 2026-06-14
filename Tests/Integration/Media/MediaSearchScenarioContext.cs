using Contracts.Media.Search;

namespace Integration.Tests.Media;

public sealed class MediaSearchScenarioContext
{
	public IReadOnlyList<IMediaDetail>? Results { get; set; }
}
