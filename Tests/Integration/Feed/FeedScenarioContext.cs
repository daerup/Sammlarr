using Contracts.Media;
using Contracts.Media.Search;

namespace Integration.Tests.Feed;

public sealed class FeedRegistration
{
	public required string Identifier { get; init; }
	public List<FeedItemSpec> Items { get; } = [];
}

public sealed record FeedItemSpec(string? Title, MediaType Type, int? Year, IReadOnlyDictionary<MediaProvider, string> Ids);

public sealed class FeedScenarioContext
{
	public Dictionary<string, FeedRegistration> Feeds { get; } = new();
	public List<IMediaDetail>? Items { get; set; }
}
