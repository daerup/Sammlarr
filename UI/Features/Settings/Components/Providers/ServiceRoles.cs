using Domain.Providers;

namespace UI.Features.Settings.Components.Providers;

internal sealed record ServiceRole(string Name, string Icon, string Description, IReadOnlySet<MediaProvider> Serves);

internal static class ServiceRoles
{
	internal static readonly ServiceRole SignIn = new(
		"Sign-in",
		"log-in",
		"Who can sign in",
		Enum.GetValues<AuthProvider>().Select(signIn => signIn.Provider()).WhereSome().ToHashSet()
	);

	internal static readonly ServiceRole Wantlist = new(
		"Wantlist",
		"bookmark",
		"Where the wantlist syncs to",
		Enum.GetValues<SinkKind>().SelectMany(sink => sink.Providers()).ToHashSet()
	);

	internal static readonly ServiceRole Search = new(
		"Search",
		"search",
		"Finding titles to add",
		Enum.GetValues<SearchProvider>().SelectMany(search => search.Providers()).ToHashSet()
	);

	internal static readonly ServiceRole Trending = new(
		"Trending",
		"trending-up",
		"What Browse shows",
		Enum.GetValues<TrendingProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet()
	);

	internal static readonly ServiceRole Feeds = new(
		"Feeds",
		"rss",
		"Imported lists; each feed picks its source",
		Enum.GetValues<FeedProvider>().Select(provider => provider.AsMediaProvider()).ToHashSet()
	);

	private static readonly IReadOnlyList<ServiceRole> All = [SignIn, Wantlist, Search, Trending, Feeds];

	internal static IReadOnlyList<ServiceRole> For(MediaProvider provider) => [.. All.Where(role => role.Serves.Contains(provider))];

	internal static bool IsAccount(MediaProvider provider) => SignIn.Serves.Contains(provider);
}
