using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Feed;

public sealed class FeedRegistration
{
	public required string Identifier { get; init; }
	public List<RegisteredMedia> Items { get; } = [];
	public Dictionary<string, string> ImdbIds { get; } = new();
}

public sealed class FeedScenarioContext
{
	public Dictionary<string, FeedRegistration> Feeds { get; } = new();
}
