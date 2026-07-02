using Contracts.Media;
using Contracts.Media.Search;
using Domain.Media;
using Domain.Providers;

namespace Integration.Tests.Feeds;

public sealed class FeedRegistration
{
	public required string Identifier { get; init; }
	public List<FeedItemSpec> Items { get; } = [];
}

public sealed record FeedItemSpec(string? Title, MediaType Type, int? Year, IReadOnlyDictionary<MediaProvider, string> Ids);

public sealed class FeedScenarioContext
{
	public Dictionary<string, FeedRegistration> Feeds { get; } = new();
	public List<MediaLookup>? Items { get; set; }
}
