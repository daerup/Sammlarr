using Contracts.Feed;
using Domain.Usage;
using Observability.Usage;

namespace UI.Features.Feeds.Pages.Editor;

internal static class FeedLinkCheck
{
	extension(IFeedProvider provider)
	{
		public async Task<Result<string>> CheckLinkAsync(string typed, CancellationToken cancellationToken = default)
		{
			using var marked = UsageCauses.Mark(UsageCause.FeedEditor);
			return await provider.NormalizeAsync(typed, cancellationToken);
		}
	}
}
