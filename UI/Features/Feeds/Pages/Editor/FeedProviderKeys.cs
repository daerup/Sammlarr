using Contracts.Auth;
using Domain.Providers;

namespace UI.Features.Feeds.Pages.Editor;

internal static class FeedProviderKeys
{
	internal static async Task<IReadOnlySet<FeedProvider>> WithoutKeyAsync(IApiKeyService keys, CancellationToken cancellationToken = default)
	{
		var missing = new HashSet<FeedProvider>();
		foreach (var provider in Enum.GetValues<FeedProvider>())
		{
			if (!await keys.GetAsync(provider.AsApiKeyProvider(), cancellationToken).IsSuccess())
			{
				missing.Add(provider);
			}
		}

		return missing;
	}
}
