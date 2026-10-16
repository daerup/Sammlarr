using Domain.Feed;

namespace UI.Features.Home.Components;

internal static class FeedLabel
{
	internal static string Of(FeedId feedId, IReadOnlyDictionary<FeedId, string> feedNames) => feedNames.GetValueOrDefault(feedId, "A deleted feed");
}
