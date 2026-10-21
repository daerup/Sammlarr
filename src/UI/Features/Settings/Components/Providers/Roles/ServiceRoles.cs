using Domain.Providers;

namespace UI.Features.Settings.Components.Providers.Roles;

internal sealed record ServiceRole(string Name, string Icon, string Description, IReadOnlySet<Service> Serves);

internal static class ServiceRoles
{
	internal static readonly ServiceRole SignIn = new(
		"Sign-in",
		"log-in",
		"Who can sign in",
		Enum.GetValues<AuthProvider>().Select(signIn => signIn.Service).ToHashSet()
	);

	internal static readonly ServiceRole Wantlist = new(
		"Wantlist",
		"bookmark",
		"Where the wantlist syncs to",
		Enum.GetValues<SinkKind>().Select(sink => sink.Service).ToHashSet()
	);

	internal static readonly ServiceRole Search = new(
		"Search",
		"search",
		"Finding titles to add",
		Enum.GetValues<SearchProvider>().Select(search => search.Service).ToHashSet()
	);

	internal static readonly ServiceRole Trending = new(
		"Trending",
		"trending-up",
		"What Browse shows",
		Enum.GetValues<TrendingProvider>().Select(trending => trending.Service).ToHashSet()
	);

	internal static readonly ServiceRole Feeds = new(
		"Feeds",
		"rss",
		"Imported lists; each feed picks its source",
		Enum.GetValues<FeedProvider>().Select(feed => feed.Service).ToHashSet()
	);

	private static readonly IReadOnlyList<ServiceRole> All = [SignIn, Wantlist, Search, Trending, Feeds];

	internal static IReadOnlyList<ServiceRole> For(Service service) => [.. All.Where(role => role.Serves.Contains(service))];
}
