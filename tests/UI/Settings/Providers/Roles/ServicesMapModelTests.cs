using System.Globalization;
using Domain.Providers;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers.Roles;

namespace UI.Tests.Settings.Providers.Roles;

public class ServicesMapModelTests
{
	[Test]
	public async Task Roles_Always_ReadSignInFirstAndFeedsLast()
	{
		var roles = ServicesMapModel.For(ProviderBranding.OnPlexSearchingPlex).Roles.Select(role => role.Name);

		await Assert.That(roles).IsEquivalentTo(["Sign-in", "Wantlist", "Search", "Trending", "Feeds"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Traces_ForTodaysProviders_LandEachOnItsOwnPinInRoleOrder()
	{
		var traces = ServicesMapModel
			.For(ProviderBranding.OnPlexSearchingPlex)
			.Traces.Select(trace => $"{trace.Role} > {trace.Provider} at {trace.Points[^1].X},{trace.Points[^1].Y}");

		await Assert
			.That(traces)
			.IsEquivalentTo([
				"Sign-in > Plex at 816,70",
				"Wantlist > Plex at 816,78",
				"Search > Plex at 816,86",
				"Trending > Tmdb at 816,182",
				"Feeds > Tmdb at 816,190",
				"Feeds > Mdblist at 398.5,276",
				"Feeds > AniList at 611.5,276",
				"Feeds > Mal at 824.5,276",
				"Feeds > Trakt at 1037.5,276",
			]);
	}

	[Test]
	public async Task Roles_WhenTheWantlistSyncsToRadarrAndSonarr_LandOnOneBoxForBoth()
	{
		var model = ServicesMapModel.For(ProviderBranding.OnPlexSearchingPlex with { Sink = SinkKind.Arr });

		var wantlist = model.Roles.Single(role => role.Name == "Wantlist");
		await Assert.That(wantlist.Provider).IsEqualTo(new Arr());
		await Assert.That(wantlist.Picker!.Options.Select(option => option.ToString())).IsEquivalentTo(["Plex", "Arr"], CollectionOrdering.Matching);
		await Assert.That(model.Services.Select(service => service.Provider.ToString())).Contains("Arr");
	}

	[Test]
	public async Task Traces_ForTheMiddleRoleOfEachService_RunStraight()
	{
		var straight = Straight(ServicesMapModel.For(ProviderBranding.OnPlexSearchingPlex));

		await Assert.That(straight).IsEquivalentTo(["Wantlist", "Trending"]);
	}

	[Test]
	public async Task Traces_ForServicesThatEachTakeOneRoleInARow_RunStraight()
	{
		var model = ServicesMapModel.For(ProviderBranding.OnPlexSearchingPlex with { AuthProvider = AuthProvider.Local, SearchProvider = SearchProvider.Tmdb });

		await Assert.That(Straight(model)).IsEquivalentTo(["Sign-in", "Wantlist", "Search"]);
	}

	[Test]
	public async Task Services_ForTodaysProviders_SitBesideTheirRolesWithAPinPerIncomingTrace()
	{
		var services = ServicesMapModel
			.For(ProviderBranding.OnPlexSearchingPlex)
			.Services.Select(service => $"{service.Provider} {service.Y}: {string.Join(" ", service.Pins)}");

		await Assert.That(services).IsEquivalentTo(["Plex 78: 70 78 86", "Tmdb 186: 182 190"]);
	}

	[Test]
	public async Task Sources_ForTodaysProviders_SitInARowUnderTheFeedsWithTheirNames()
	{
		var sources = ServicesMapModel
			.For(ProviderBranding.OnPlexSearchingPlex)
			.Sources.Select(source => $"{source.Provider} {source.X},{source.Y} {source.Width} {(source.Named ? "named" : "logo")}");

		await Assert
			.That(sources)
			.IsEquivalentTo(
				["Mdblist 300,276 197 named", "AniList 513,276 197 named", "Mal 726,276 197 named", "Trakt 939,276 197 named"],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task PlaceSources_ForTwo_KeepsTheirWidthFromTheSameEdgeUnderTheLowestPin()
	{
		var sources = ServicesMapModel.PlaceSources([RemoteService.Mdblist, RemoteService.Trakt], 100);

		await Assert
			.That(sources.Select(source => $"{source.X},{source.Y} {source.Width}"))
			.IsEquivalentTo(["300,126 200", "516,126 200"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task PlaceSources_ForSix_NarrowsThemToTheirLogos()
	{
		var sources = ServicesMapModel.PlaceSources([.. Enumerable.Repeat<Service>(RemoteService.Mdblist, 6)], 100);

		await Assert.That(sources.Select(source => source.Width).Distinct()).IsEquivalentTo([126d]);
		await Assert.That(sources.Any(source => source.Named)).IsFalse();
	}

	[Test]
	public async Task FeedLines_ForTodaysProviders_LeaveAPinEachWithTheBoxedLineOnTopAndTheLeftmostSourceAtTheBottom()
	{
		var pins = ServicesMapModel
			.For(ProviderBranding.OnPlexSearchingPlex)
			.FeedLines.OrderBy(line => line.Points[0].Y)
			.Select(line => $"{line.Provider} {line.Points[0].X},{line.Points[0].Y}");

		await Assert.That(pins).IsEquivalentTo(["Tmdb 256,218", "Trakt 256,226", "Mal 256,234", "AniList 256,242", "Mdblist 256,250"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Traces_ToEachSource_RunStraightFromTheirPinAndDropIntoTheMiddleOfTheirSource()
	{
		var model = ServicesMapModel.For(ProviderBranding.OnPlexSearchingPlex);

		var trakt = model.Traces.Single(trace => trace.Provider == RemoteService.Trakt);
		var source = model.Sources.Single(candidate => candidate.Provider == RemoteService.Trakt);
		var middle = source.X + source.Width / 2;
		await Assert
			.That(trakt.Points)
			.IsEquivalentTo([new MapPoint(256, 226), new MapPoint(middle, 226), new MapPoint(middle, source.Y)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task FeedLines_BeyondWhatTheFeedsRowHolds_GrowDownwardAndPushTheSourcesLower()
	{
		var model = ServicesMapModel.For(ProviderBranding.OnPlexSearchingPlex, [RemoteService.Tmdb, .. Enumerable.Repeat<Service>(RemoteService.Mdblist, 8)]);

		await Assert.That(model.FeedLines.Min(line => line.Points[0].Y)).IsEqualTo(218);
		await Assert.That(model.Sources[0].Y).IsEqualTo(308);
	}

	[Test]
	public async Task Traces_WhenSearchAlsoUsesTmdb_NeverCross()
	{
		var model = ServicesMapModel.For(ProviderBranding.OnPlexSearchingPlex with { SearchProvider = SearchProvider.Tmdb });

		await Assert.That(Crossings(model)).IsEmpty();
	}

	[Test]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(8)]
	[Arguments(9)]
	[Arguments(16)]
	[Arguments(17)]
	public async Task Traces_ForAnyNumberOfFeedSources_NeverCross(int sources)
	{
		var model = ServicesMapModel.For(ProviderBranding.OnPlexSearchingPlex, [RemoteService.Tmdb, .. Enumerable.Repeat<Service>(RemoteService.Mdblist, sources)]);

		await Assert.That(Crossings(model)).IsEmpty();
	}

	[Test]
	[Arguments(0, 0, 20, 0, 20, 20, "M0 0 L14 0 L20 6 L20 20")]
	[Arguments(0, 0, 10, 0, 10, 4, "M0 0 L8 0 L10 2 L10 4")]
	[Arguments(0, 0, 10, 0, 10, 3.3, "M0 0 L8.35 0 L10 1.65 L10 3.3")]
	public async Task Path_ForAnLShapedRun_CutsTheCornerAt45Degrees(double x0, double y0, double x1, double y1, double x2, double y2, string path)
	{
		var trace = new MapTrace("Feeds", RemoteService.Tmdb, [new(x0, y0), new(x1, y1), new(x2, y2)]);

		await Assert.That(trace.Path).IsEqualTo(path);
	}

	[Test]
	public async Task Path_UnderACommaDecimalCulture_StillWritesDots()
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
		var trace = new MapTrace("Feeds", RemoteService.Tmdb, [new(0, 0.5), new(10.25, 0.5)]);

		await Assert.That(trace.Path).IsEqualTo("M0 0.5 L10.25 0.5");
	}

	[Test]
	public async Task Path_ForARepeatedPoint_DrawsItOnce()
	{
		var trace = new MapTrace("Wantlist", RemoteService.Plex, [new(0, 0), new(10, 0), new(10, 0), new(20, 0)]);

		await Assert.That(trace.Path).IsEqualTo("M0 0 L5 0 L15 0 L20 0");
	}

	private static IEnumerable<string> Straight(ServicesMapModel model) =>
		model
			.Traces.Where(trace => trace.Points.Select(point => point.Y).Distinct().Count() == 1)
			.Select(trace => trace.Role == "Feeds" ? $"{trace.Role} > {trace.Provider}" : trace.Role);

	private static IEnumerable<(int First, int Second)> Crossings(ServicesMapModel model)
	{
		var runs = model.Traces.Select(trace => trace.Points.Zip(trace.Points.Skip(1)).ToList()).ToList();
		return from first in Enumerable.Range(0, runs.Count)
			from second in Enumerable.Range(first + 1, runs.Count - first - 1)
			from a in runs[first]
			from b in runs[second]
			where Crosses(a, b) || Crosses(b, a)
			select (first, second);
	}

	private static bool Crosses((MapPoint First, MapPoint Second) horizontal, (MapPoint First, MapPoint Second) vertical) =>
		horizontal.First.Y == horizontal.Second.Y
		&& vertical.First.X == vertical.Second.X
		&& Between(horizontal.First.Y, vertical.First.Y, vertical.Second.Y)
		&& Between(vertical.First.X, horizontal.First.X, horizontal.Second.X);

	private static bool Between(double value, double from, double to) => value > Math.Min(from, to) && value < Math.Max(from, to);
}
