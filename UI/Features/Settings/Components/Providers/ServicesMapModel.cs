using Domain.Providers;
using Domain.Settings;

namespace UI.Features.Settings.Components.Providers;

internal enum TileEdge
{
	Bottom,
	Top,
}

internal sealed record MapPoint(double X, double Y);

internal union MapEndpoint(ServiceEndpoint, SammlarrAccounts)
{
	public static implicit operator MapEndpoint(MediaProvider provider) => new ServiceEndpoint(provider, new None());

	public static MapEndpoint Of(AuthProvider signIn) => signIn.Provider().Map(provider => (MapEndpoint)provider).GetValueOr(new SammlarrAccounts());

	public static MapEndpoint Of(SinkKind sink) => ServiceEndpoint.Of(sink.Providers());

	public static MapEndpoint Of(SearchProvider search) => ServiceEndpoint.Of(search.Providers());

	public static bool operator ==(MapEndpoint left, MapEndpoint right) => Equals(left.Value, right.Value);

	public static bool operator !=(MapEndpoint left, MapEndpoint right) => !Equals(left.Value, right.Value);

	public override bool Equals(object? obj) => obj is MapEndpoint other && this == other;

	public override int GetHashCode() => Value?.GetHashCode() ?? 0;

	public override string ToString()
	{
		return this switch
		{
			ServiceEndpoint service => service.ToString(),
			SammlarrAccounts => "Sammlarr",
		};
	}
}

internal readonly record struct ServiceEndpoint(MediaProvider Provider, Optional<MediaProvider> Partner)
{
	public IReadOnlyList<MediaProvider> Providers
	{
		get
		{
			var provider = Provider;
			return Partner.Match(partner => (IReadOnlyList<MediaProvider>)[provider, partner], () => [provider]);
		}
	}

	public static ServiceEndpoint Of(IReadOnlyList<MediaProvider> providers) => new(providers[0], providers.Skip(1).FirstOrNone());

	public override string ToString() => string.Join("+", Providers);
}

internal enum MapChoice
{
	Wantlist,
	Search,
}

internal sealed record MapPicker(MapChoice Choice, string Label, IReadOnlyList<MapEndpoint> Options, MapEndpoint Current);

internal sealed record MapRole(string Name, string Icon, string Description, double Y, MapEndpoint? Provider, MapPicker? Picker);

internal sealed record MapService(MapEndpoint Provider, double Y, IReadOnlyList<double> Pins);

internal sealed record MapTile(MediaProvider Provider, double X, double Y, TileEdge Entry);

internal sealed record MapTrace(string Role, MapEndpoint Provider, IReadOnlyList<MapPoint> Points)
{
	internal string Path => TracePath.Draw(Points);
}

internal static class TracePath
{
	private const double CornerCut = 6;

	internal static string Draw(IReadOnlyList<MapPoint> points)
	{
		var path = points.Where((point, index) => index == 0 || point != points[index - 1]).ToList();
		var corners = path.Skip(1).SkipLast(1).Select((corner, index) => Cut(path[index], corner, path[index + 2]));
		return string.Join(" ", [Command("M", path[0]), .. corners, Command("L", path[^1])]);
	}

	private static string Cut(MapPoint from, MapPoint corner, MapPoint to)
	{
		var before = Distance(from, corner);
		var after = Distance(corner, to);
		var cut = Math.Min(CornerCut, Math.Min(before, after) / 2);
		return $"{Command("L", Toward(corner, from, cut / before))} {Command("L", Toward(corner, to, cut / after))}";
	}

	private static double Distance(MapPoint from, MapPoint to) => Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));

	private static MapPoint Toward(MapPoint from, MapPoint to, double share) => new(from.X + (to.X - from.X) * share, from.Y + (to.Y - from.Y) * share);

	private static string Command(string command, MapPoint point) => FormattableString.Invariant($"{command}{Math.Round(point.X, 2)} {Math.Round(point.Y, 2)}");
}

internal sealed record ServicesMapModel(
	IReadOnlyList<MapRole> Roles,
	IReadOnlyList<MapService> Services,
	IReadOnlyList<MapTile> Tiles,
	IReadOnlyList<MapTrace> Traces,
	double Height
)
{
	internal const double RoleWidth = 256;
	internal const double ServiceX = RoleWidth + LineGap;
	internal const double Width = ServiceX + ServiceWidth;
	private const double RowHeight = 48;
	internal const double ServiceHeight = 44;
	private const double TileSize = 28;
	internal const double ChipX = RoleWidth + LineGap / 4;
	private const double LineGap = 340;
	private const double JogX = RoleWidth + LineGap / 2;
	private const double ServiceWidth = 266;
	private const double ServiceGap = 8;
	private const double TileGap = 5;
	private const double TilePitch = TileSize + TileGap;
	private const int RowCapacity = (int)((ServiceWidth + TileGap) / TilePitch);
	private const double PinPitch = 8;
	private const double RibbonPitch = 3.8;
	private const double Clearance = 8;
	private const double LaneMargin = 4;
	private const double SplitLead = 18;
	private const double RiseInset = 16;
	private const double RiseLead = 10;

	internal static ServicesMapModel For(ProviderConfig config) =>
		For(config, [.. Enum.GetValues<FeedProvider>().Select(provider => provider.AsMediaProvider())]);

	internal static ServicesMapModel For(ProviderConfig config, IReadOnlyList<MediaProvider> feeds)
	{
		var roles = RolesFor(config);
		var landings = roles
			.SelectMany(role =>
				(role.Provider is { } provider ? [provider] : feeds.Select(feed => (MapEndpoint)feed)).Select(provider => (Role: role, Provider: provider))
			)
			.ToList();
		var services = Place(roles, landings);

		double Pin(MapRole role, MapEndpoint provider)
		{
			var arriving = landings.Where(landing => landing.Provider == provider).Select(landing => landing.Role).ToList();
			return services.Single(service => service.Provider == provider).Pins[arriving.IndexOf(role)];
		}

		var boxed = feeds.Where(feed => services.Any(service => service.Provider == (MapEndpoint)feed)).ToList();
		var tiles = PlaceTiles([.. feeds.Where(feed => !boxed.Contains(feed))], services[^1].Y + ServiceHeight / 2 + ServiceGap, feeds.Count);
		var feedsRole = roles.Single(role => role.Provider is null);

		List<MapTrace> traces =
		[
			.. landings
				.Where(landing => landing.Role.Provider is not null)
				.Select(landing =>
				{
					var pin = Pin(landing.Role, landing.Provider);
					return new MapTrace(
						landing.Role.Name,
						landing.Provider,
						[new(RoleWidth, landing.Role.Y), new(JogX, landing.Role.Y), new(JogX, pin), new(ServiceX, pin)]
					);
				}),
			.. FeedTraces(feedsRole, [.. boxed.Select(provider => (provider, new MapPoint(ServiceX, Pin(feedsRole, provider))))], tiles),
		];

		var height = new[]
		{
			roles.Count * RowHeight,
			services.Max(service => service.Y + ServiceHeight / 2),
			tiles.Select(tile => tile.Y + TileSize + 2).DefaultIfEmpty().Max(),
			traces.SelectMany(trace => trace.Points).Max(point => point.Y) + 6,
		}.Max();

		return new ServicesMapModel(roles, services, tiles, traces, height);
	}

	internal static IReadOnlyList<MapTile> PlaceTiles(IReadOnlyList<MediaProvider> sources, double top, int lines)
	{
		if (sources.Count <= RowCapacity)
		{
			return [.. sources.Select((source, index) => new MapTile(source, ServiceX + index * TilePitch, top, TileEdge.Bottom))];
		}

		var upper = (sources.Count + 1) / 2;
		var left = upper <= RowCapacity ? ServiceX : Width - (upper * TilePitch - TileGap);
		var lower = top + TileSize + (lines - 1) * RibbonPitch + 2 * Clearance + LaneMargin;
		return
		[
			.. sources.Select(
				(source, index) =>
					index < upper
						? new MapTile(source, left + index * TilePitch, top, TileEdge.Bottom)
						: new MapTile(source, left + (index - upper) * TilePitch, lower, TileEdge.Top)
			),
		];
	}

	private static List<MapRole> RolesFor(ProviderConfig config)
	{
		(ServiceRole Role, MapEndpoint? Provider, MapPicker? Picker)[] rows =
		[
			(ServiceRoles.SignIn, MapEndpoint.Of(config.AuthProvider), null),
			(
				ServiceRoles.Wantlist,
				MapEndpoint.Of(config.Sink),
				Picker(MapChoice.Wantlist, "Wantlist syncs to", Enum.GetValues<SinkKind>().Select(MapEndpoint.Of), MapEndpoint.Of(config.Sink))
			),
			(
				ServiceRoles.Search,
				MapEndpoint.Of(config.SearchProvider),
				Picker(MapChoice.Search, "Search uses", Enum.GetValues<SearchProvider>().Select(MapEndpoint.Of), MapEndpoint.Of(config.SearchProvider))
			),
			(ServiceRoles.Trending, config.TrendingProvider.AsMediaProvider(), null),
			(ServiceRoles.Feeds, null, null),
		];
		return
		[
			.. rows.Select(
				(row, index) => new MapRole(row.Role.Name, row.Role.Icon, row.Role.Description, index * RowHeight + RowHeight / 2, row.Provider, row.Picker)
			),
		];
	}

	private static MapPicker? Picker(MapChoice choice, string label, IEnumerable<MapEndpoint> options, MapEndpoint current)
	{
		return options.ToList() is { Count: > 1 } choices ? new MapPicker(choice, label, choices, current) : null;
	}

	private static List<MapService> Place(List<MapRole> roles, List<(MapRole Role, MapEndpoint Provider)> landings)
	{
		var services = new List<MapService>();
		foreach (
			var solid in roles
				.Where(role => role.Provider.HasValue)
				.GroupBy(role => role.Provider.GetValueOrDefault())
				.OrderBy(group => group.Average(role => role.Y))
		)
		{
			var arriving = landings.Where(landing => landing.Provider == solid.Key).Select(landing => landing.Role).ToList();
			var straight = solid.ElementAt((solid.Count() - 1) / 2);
			var floor = services.Count == 0 ? ServiceHeight / 2 : services[^1].Y + ServiceHeight + ServiceGap;
			var y = Math.Max(straight.Y - PinOffset(arriving.IndexOf(straight), arriving.Count), floor);
			services.Add(new MapService(solid.Key, y, [.. arriving.Select((_, index) => y + PinOffset(index, arriving.Count))]));
		}

		return services;
	}

	private static double PinOffset(int index, int count) => (index - (count - 1) / 2.0) * PinPitch;

	private static IEnumerable<MapTrace> FeedTraces(MapRole feeds, IReadOnlyList<(MediaProvider Provider, MapPoint Pin)> boxed, IReadOnlyList<MapTile> tiles)
	{
		List<(MediaProvider Provider, MapPoint Entry, TileEdge Edge)> peels =
		[
			.. tiles.Where(tile => tile.Entry == TileEdge.Bottom).OrderBy(tile => tile.X).Select(Peel),
			.. tiles.Where(tile => tile.Entry == TileEdge.Top).OrderByDescending(tile => tile.X).Select(Peel),
		];
		var centre = Centre(feeds.Y, peels);
		var tracks = peels.Select((_, index) => centre + (index - (peels.Count - 1) / 2.0) * RibbonPitch).ToList();
		var below = tracks.DefaultIfEmpty(double.PositiveInfinity).First();
		List<(MediaProvider Provider, MapPoint Entry, bool Boxed, double Track)> lines =
		[
			.. boxed.Select((box, index) => (box.Provider, box.Pin, true, Math.Min(feeds.Y, below - (boxed.Count - index) * RibbonPitch))),
			.. peels.Select((peel, index) => (peel.Provider, peel.Entry, false, tracks[index])),
		];
		var deepest = lines.Max(line => Math.Abs(line.Track - feeds.Y));
		var rise = tiles.Select(tile => tile.X - RiseLead).Prepend(ServiceX - RiseInset).Min();
		var split = lines.Select(line => line.Boxed ? rise : line.Entry.X).Min() - deepest - SplitLead;

		return lines.Select(line =>
		{
			var turn = split + deepest - Math.Abs(line.Track - feeds.Y);
			MapPoint[] strand = [new(RoleWidth, feeds.Y), new(turn, feeds.Y), new(turn, line.Track)];
			MapPoint[] landing = line.Boxed ? [new(rise, line.Track), new(rise, line.Entry.Y), line.Entry] : [new(line.Entry.X, line.Track), line.Entry];
			return new MapTrace(feeds.Name, line.Provider, [.. strand, .. landing]);
		});
	}

	private static double Centre(double feedsY, List<(MediaProvider Provider, MapPoint Entry, TileEdge Edge)> peels)
	{
		var middle = (peels.Count - 1) / 2.0;
		var floor = peels
			.Select((peel, index) => peel.Edge == TileEdge.Bottom ? peel.Entry.Y + Clearance - (index - middle) * RibbonPitch : double.NegativeInfinity)
			.DefaultIfEmpty(double.NegativeInfinity)
			.Max();
		var ceiling = peels
			.Select((peel, index) => peel.Edge == TileEdge.Top ? peel.Entry.Y - Clearance - (index - middle) * RibbonPitch : double.PositiveInfinity)
			.DefaultIfEmpty(double.PositiveInfinity)
			.Min();
		return Math.Min(Math.Max(feedsY, floor), ceiling);
	}

	private static (MediaProvider Provider, MapPoint Entry, TileEdge Edge) Peel(MapTile tile) =>
		(tile.Provider, new MapPoint(tile.X + TileSize / 2, tile.Entry == TileEdge.Bottom ? tile.Y + TileSize : tile.Y), tile.Entry);
}
