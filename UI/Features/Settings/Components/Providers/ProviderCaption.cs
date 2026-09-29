using Domain.Providers;

namespace UI.Features.Settings.Components.Providers;

internal static class ProviderCaption
{
	private static readonly IReadOnlySet<MediaProvider> Accounts = Enum.GetValues<AuthProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet();

	private static readonly (IReadOnlySet<MediaProvider> Providers, string Word)[] Uses =
	[
		(Enum.GetValues<SearchProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet(), "search"),
		(Enum.GetValues<WatchlistProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet(), "watchlist"),
		(Enum.GetValues<TrendingProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet(), "trending"),
		(Enum.GetValues<RatingProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet(), "ratings"),
		(Enum.GetValues<FeedProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet(), "feeds"),
		(Accounts, "sign-in"),
	];

	internal static string For(MediaProvider provider)
	{
		var credential = Accounts.Contains(provider) ? "Account" : "API key";
		var uses = Uses.Where(use => use.Providers.Contains(provider)).Select(use => use.Word);
		return $"{credential} · {string.Join(", ", uses)}";
	}
}
