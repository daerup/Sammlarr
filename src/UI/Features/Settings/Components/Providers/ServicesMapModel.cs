using Domain.Providers;
using Domain.Settings;

namespace UI.Features.Settings.Components.Providers;

internal sealed record MapPoint(double X, double Y);

internal enum MapChoice
{
	Wantlist,
	Search,
}

internal sealed record MapPicker(MapChoice Choice, string Label, IReadOnlyList<Service> Options, Service Current);

internal sealed record MapRole(string Name, string Icon, string Description, double Y, Service? Provider, MapPicker? Picker);

internal sealed record MapService(Service Provider, double Y, IReadOnlyList<double> Pins);

internal sealed record MapSource(Service Provider, double X, double Y, double Width)
{
	internal bool Named => Width >= ServicesMapModel.NamedSourceWidth;
}

internal sealed record MapTrace(string Role, Service Provider, IReadOnlyList<MapPoint> Points)
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
	IReadOnlyList<MapSource> Sources,
	IReadOnlyList<MapTrace> Traces,
	double Height
)
{
	internal const double RoleWidth = 256;
	internal const double ServiceX = RoleWidth + LineGap;
	internal const double Width = ServiceX + ServiceWidth;
	internal const double CardWidth = Width + 2 * CardPadding + 2;
	private const double RowHeight = ServiceHeight + ServiceGap;
	internal const double ServiceHeight = 44;
	internal const double NamedSourceWidth = 150;
	internal const double ChipX = RoleWidth + LineGap / 4;
	private const double LineGap = 560;
	private const double JogX = RoleWidth + LineGap / 2;
	internal const double ServiceWidth = 320;
	private const double ServiceGap = 8;
	private const double CardPadding = 16;
	private const double PinPitch = 8;
	private const double SourceX = RoleWidth + 44;
	private const double SourceGap = 16;
	private const double MaxSourceWidth = 200;
	private const double SourceDrop = 26;
	private const double RiseX = ServiceX - 16;
	private const double RisePitch = 6;
	private const double FeedPinRise = 16;

	internal IReadOnlyList<MapTrace> FeedLines => [.. Traces.Where(trace => trace.Role == ServiceRoles.Feeds.Name)];

	internal static ServicesMapModel For(ProviderConfig config) => For(config, [.. Enum.GetValues<FeedProvider>().Select(feed => feed.Service).Distinct()]);

	internal static ServicesMapModel For(ProviderConfig config, IReadOnlyList<Service> feeds)
	{
		var roles = RolesFor(config);
		var landings = roles.SelectMany(role => (role.Provider is { } provider ? [provider] : feeds).Select(provider => (Role: role, Provider: provider))).ToList();
		var services = Place(roles, landings);

		double Pin(MapRole role, Service provider)
		{
			var arriving = landings.Where(landing => landing.Provider == provider).Select(landing => landing.Role).ToList();
			return services.Single(service => service.Provider == provider).Pins[arriving.IndexOf(role)];
		}

		var feedsRole = roles.Single(role => role.Provider is null);
		var boxed = feeds.Where(feed => services.Any(service => service.Provider == feed)).ToList();
		var unboxed = feeds.Where(feed => !boxed.Contains(feed)).ToList();
		var lines = feeds.Count;
		var sources = PlaceSources(unboxed, FeedPin(feedsRole, lines - 1, lines));

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
			.. BoxedTraces(feedsRole, [.. boxed.Select(provider => (provider, Pin(feedsRole, provider)))], lines),
			.. SourceTraces(feedsRole, sources, lines),
		];

		var height = new[]
		{
			roles.Count * RowHeight,
			services.Max(service => service.Y + ServiceHeight / 2),
			sources.Select(source => source.Y + ServiceHeight + 2).DefaultIfEmpty().Max(),
			traces.SelectMany(trace => trace.Points).Max(point => point.Y) + 6,
		}.Max();

		return new ServicesMapModel(roles, services, sources, traces, height);
	}

	internal static IReadOnlyList<MapSource> PlaceSources(IReadOnlyList<Service> sources, double lowestPin)
	{
		var top = lowestPin + SourceDrop;
		var width = Math.Min(MaxSourceWidth, (Width - SourceX - Math.Max(0, sources.Count - 1) * SourceGap) / Math.Max(1, sources.Count));
		return [.. sources.Select((source, index) => new MapSource(source, SourceX + index * (width + SourceGap), top, width))];
	}

	private static IEnumerable<MapTrace> BoxedTraces(MapRole feeds, IReadOnlyList<(Service Provider, double Pin)> boxed, int lines)
	{
		var ordered = boxed.OrderBy(box => box.Pin).ToList();
		return ordered.Select(
			(box, index) =>
			{
				var y = FeedPin(feeds, index, lines);
				var rise = RiseX - (ordered.Count - 1 - index) * RisePitch;
				return new MapTrace(feeds.Name, box.Provider, [new(RoleWidth, y), new(rise, y), new(rise, box.Pin), new(ServiceX, box.Pin)]);
			}
		);
	}

	private static IEnumerable<MapTrace> SourceTraces(MapRole feeds, IReadOnlyList<MapSource> sources, int lines)
	{
		return sources.Select(
			(source, index) =>
			{
				var y = FeedPin(feeds, lines - 1 - index, lines);
				var middle = source.X + source.Width / 2;
				return new MapTrace(feeds.Name, source.Provider, [new(RoleWidth, y), new(middle, y), new(middle, source.Y)]);
			}
		);
	}

	private static List<MapRole> RolesFor(ProviderConfig config)
	{
		(ServiceRole Role, Service? Provider, MapPicker? Picker)[] rows =
		[
			(ServiceRoles.SignIn, config.AuthProvider.Service, null),
			(
				ServiceRoles.Wantlist,
				config.Sink.Service,
				Picker(MapChoice.Wantlist, "Wantlist syncs to", Enum.GetValues<SinkKind>().Select(sink => sink.Service), config.Sink.Service)
			),
			(
				ServiceRoles.Search,
				config.SearchProvider.Service,
				Picker(MapChoice.Search, "Search uses", Enum.GetValues<SearchProvider>().Select(search => search.Service), config.SearchProvider.Service)
			),
			(ServiceRoles.Trending, config.TrendingProvider.Service, null),
			(ServiceRoles.Feeds, null, null),
		];
		return
		[
			.. rows.Select(
				(row, index) => new MapRole(row.Role.Name, row.Role.Icon, row.Role.Description, index * RowHeight + RowHeight / 2, row.Provider, row.Picker)
			),
		];
	}

	private static MapPicker? Picker(MapChoice choice, string label, IEnumerable<Service> options, Service current)
	{
		return options.ToList() is { Count: > 1 } choices ? new MapPicker(choice, label, choices, current) : null;
	}

	private static List<MapService> Place(List<MapRole> roles, List<(MapRole Role, Service Provider)> landings)
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

	private static double FeedPin(MapRole feeds, int index, int lines) => feeds.Y + Math.Max(PinOffset(0, lines), -FeedPinRise) + index * PinPitch;
}
