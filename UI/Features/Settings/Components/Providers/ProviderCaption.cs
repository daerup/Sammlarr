using Domain.Providers;

namespace UI.Features.Settings.Components.Providers;

internal static class ProviderCaption
{
	private static readonly IReadOnlySet<MediaProvider> Accounts = Enum.GetValues<AuthProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet();

	private static readonly (IReadOnlySet<MediaProvider> Providers, string Word)[] Uses =
	[
		(Enum.GetValues<SearchProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet(), "search"),
		(Enum.GetValues<SinkKind>().SelectMany(sink => sink.Providers()).ToHashSet(), "wantlist"),
		(Enum.GetValues<TrendingProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet(), "trending"),
		(Enum.GetValues<FeedProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet(), "feeds"),
		(Accounts, "sign-in"),
	];

	internal static bool IsAccount(MediaProvider provider) => Accounts.Contains(provider);

	internal static string For(MediaProvider provider)
	{
		var credential = IsAccount(provider) ? "Account" : "API key";
		var uses = Uses.Where(use => use.Providers.Contains(provider)).Select(use => use.Word);
		return $"{credential} · {string.Join(", ", uses)}";
	}
}
