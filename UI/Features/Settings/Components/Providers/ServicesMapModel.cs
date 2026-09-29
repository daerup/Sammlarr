using Domain.Providers;
using Domain.Settings;

namespace UI.Features.Settings.Components.Providers;

internal sealed record MapRole(string Name, string Icon, string Description, double Y, IReadOnlyList<MediaProvider> Providers, bool PerFeed);

internal sealed record MapService(MediaProvider Provider, double Y, bool IsFeedSource);

internal sealed record MapLine(MediaProvider Provider, double From, double To, bool PerFeed);

internal sealed record ServicesMapModel(IReadOnlyList<MapRole> Roles, IReadOnlyList<MapService> Services, IReadOnlyList<MapLine> Lines)
{
	internal const double RowHeight = 48;
	internal const double ServiceHeight = 44;
	internal const double SourceHeight = 26;
	internal const double CaptionHeight = 18;
	private const double ServiceGap = 8;
	private const double SourceGap = 4;
	private const double SourcesGap = 14;

	internal double Height => Math.Max(Roles.Count * RowHeight, Services.Max(service => service.Y + (service.IsFeedSource ? SourceHeight : ServiceHeight) / 2));

	internal double? SourcesCaptionY => Services.FirstOrDefault(service => service.IsFeedSource) is { } first ? first.Y - SourceHeight / 2 - CaptionHeight : null;

	internal static ServicesMapModel For(ProviderConfig config)
	{
		MediaProvider[] feeds = [.. Enum.GetValues<FeedProvider>().Select(provider => provider.AsMediaProvider())];
		(string Name, string Icon, string Description, MediaProvider[] Providers, bool PerFeed)[] rows =
		[
			("Watchlist", "bookmark", "Where the watchlist syncs to", [config.WatchlistProvider.AsMediaProvider()], false),
			("Search", "search", "Finding titles to add", [config.SearchProvider.AsMediaProvider()], false),
			("Sign-in", "log-in", "Who can sign in", [config.AuthProvider.AsMediaProvider()], false),
			("Trending", "trending-up", "What Browse shows", [config.TrendingProvider.AsMediaProvider()], false),
			("Ratings", "star", "Scores, filters and sorting", [config.RatingProvider.AsMediaProvider()], false),
			("Feeds", "rss", "Imported lists; each feed picks its source", feeds, true),
		];
		var roles = rows.Select((row, index) => new MapRole(row.Name, row.Icon, row.Description, index * RowHeight + RowHeight / 2, row.Providers, row.PerFeed))
			.ToList();
		var services = Place(roles);
		var lines = roles
			.SelectMany(role =>
				role.Providers.Select(provider => new MapLine(provider, role.Y, services.Single(service => service.Provider == provider).Y, role.PerFeed))
			)
			.ToList();
		return new ServicesMapModel(roles, services, lines);
	}

	private static List<MapService> Place(List<MapRole> roles)
	{
		var anchors = roles
			.SelectMany(role => role.Providers.Select(provider => (Provider: provider, Role: role)))
			.GroupBy(pair => pair.Provider)
			.Select(group => (Provider: group.Key, Solid: group.Where(pair => !pair.Role.PerFeed).Select(pair => pair.Role.Y).ToList()))
			.ToList();

		var placed = new List<MapService>();
		foreach (var shared in anchors.Where(anchor => anchor.Solid.Count > 0).OrderBy(anchor => anchor.Solid.Average()))
		{
			var floor = placed.Count == 0 ? ServiceHeight / 2 : placed[^1].Y + ServiceHeight + ServiceGap;
			placed.Add(new MapService(shared.Provider, Math.Max(shared.Solid.Average(), floor), IsFeedSource: false));
		}

		var top = (placed.Count == 0 ? 0 : placed[^1].Y + ServiceHeight / 2 + SourcesGap) + CaptionHeight;
		foreach (var source in anchors.Where(anchor => anchor.Solid.Count == 0))
		{
			placed.Add(new MapService(source.Provider, top + SourceHeight / 2, IsFeedSource: true));
			top += SourceHeight + SourceGap;
		}

		return placed;
	}
}
