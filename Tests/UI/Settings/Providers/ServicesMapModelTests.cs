using System.Globalization;
using Domain.Providers;
using Domain.Settings;
using TUnit.Assertions.Enums;
using UI.Features.Settings.Components.Providers;

namespace UI.Tests.Settings.Providers;

public class ServicesMapModelTests
{
	private static readonly ProviderConfig Config = new()
	{
		Sink = SinkKind.Plex,
		TrendingProvider = TrendingProvider.Tmdb,
		SearchProvider = SearchProvider.Plex,
		AuthProvider = AuthProvider.Plex,
		RegistrationMode = RegistrationMode.Open,
	};

	[Test]
	public async Task Roles_Always_ReadSignInFirstAndFeedsLast()
	{
		var roles = ServicesMapModel.For(Config).Roles.Select(role => role.Name);

		await Assert.That(roles).IsEquivalentTo(["Sign-in", "Wantlist", "Search", "Trending", "Feeds"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Roles_ForTodaysChoices_OfferAPickerOnlyWhereThereIsASecondOption()
	{
		var pickers = ServicesMapModel.For(Config).Roles.Where(role => role.Picker is not null).Select(role => role.Name);

		await Assert.That(pickers).IsEquivalentTo(["Wantlist", "Search"]);
	}

	[Test]
	[Arguments(AuthProvider.Plex)]
	[Arguments(AuthProvider.Local)]
	public async Task Roles_SignIn_NeverOffersAPicker(AuthProvider signIn)
	{
		var role = ServicesMapModel.For(Config with { AuthProvider = signIn }).Roles.Single(candidate => candidate.Name == "Sign-in");

		await Assert.That(role.Picker).IsNull();
	}

	[Test]
	public async Task Traces_ForTodaysProviders_LandEachOnItsOwnPinInRoleOrder()
	{
		var traces = ServicesMapModel.For(Config).Traces.Select(trace => $"{trace.Role} > {trace.Provider} at {trace.Points[^1].X},{trace.Points[^1].Y}");

		await Assert
			.That(traces)
			.IsEquivalentTo([
				"Sign-in > Plex at 596,64",
				"Wantlist > Plex at 596,72",
				"Search > Plex at 596,80",
				"Trending > Tmdb at 596,168",
				"Feeds > Tmdb at 596,176",
				"Feeds > Mdblist at 610,230",
			]);
	}

	[Test]
	public async Task Roles_WhenTheWantlistSyncsToRadarrAndSonarr_LandOnOneBoxForBoth()
	{
		var model = ServicesMapModel.For(Config with { Sink = SinkKind.Arr });

		var wantlist = model.Roles.Single(role => role.Name == "Wantlist");
		await Assert.That(wantlist.Provider).IsEqualTo(new MapEndpoint(MediaProvider.Radarr, Some.Of(MediaProvider.Sonarr)));
		await Assert.That(wantlist.Picker!.Options.Select(option => option.ToString())).IsEquivalentTo(["Plex", "Radarr+Sonarr"], CollectionOrdering.Matching);
		await Assert.That(model.Services.Select(service => service.Provider.ToString())).Contains("Radarr+Sonarr");
	}

	[Test]
	public async Task Traces_ForTheMiddleRoleOfEachService_RunStraight()
	{
		var straight = ServicesMapModel
			.For(Config)
			.Traces.Where(trace => trace.Points.Select(point => point.Y).Distinct().Count() == 1)
			.Select(trace => trace.Role);

		await Assert.That(straight).IsEquivalentTo(["Wantlist", "Trending"]);
	}

	[Test]
	public async Task Traces_ForAFeedSourceWithABox_StayOnTheFeedsRowUntilTheyRise()
	{
		var tmdb = ServicesMapModel.For(Config).Traces.Single(trace => trace.Role == "Feeds" && trace.Provider == MediaProvider.Tmdb);

		await Assert.That(tmdb.Points.Max(point => point.Y)).IsEqualTo(216);
	}

	[Test]
	public async Task Services_ForTodaysProviders_SitBesideTheirRolesWithAPinPerIncomingTrace()
	{
		var services = ServicesMapModel.For(Config).Services.Select(service => $"{service.Provider} {service.Y}: {string.Join(" ", service.Pins)}");

		await Assert.That(services).IsEquivalentTo(["Plex 72: 64 72 80", "Tmdb 172: 168 176"]);
	}

	[Test]
	public async Task Tiles_ForTodaysProviders_PutMdblistAloneUnderTmdb()
	{
		var tiles = ServicesMapModel.For(Config).Tiles.Select(tile => $"{tile.Provider} {tile.X},{tile.Y} {tile.Entry}");

		await Assert.That(tiles).IsEquivalentTo(["Mdblist 596,202 Bottom"]);
	}

	[Test]
	public async Task PlaceTiles_ForEightSources_FillsTheTopRow()
	{
		var tiles = ServicesMapModel.PlaceTiles([.. Enumerable.Repeat(MediaProvider.Mdblist, 8)], 0, 9);

		await Assert
			.That(tiles.Select(tile => $"{tile.X} {tile.Entry}"))
			.IsEquivalentTo(["596 Bottom", "629 Bottom", "662 Bottom", "695 Bottom", "728 Bottom", "761 Bottom", "794 Bottom", "827 Bottom"]);
	}

	[Test]
	public async Task PlaceTiles_ForNineSources_RebalancesRowByRow()
	{
		MediaProvider[] sources =
		[
			MediaProvider.Tmdb,
			MediaProvider.Imdb,
			MediaProvider.Tvdb,
			MediaProvider.Plex,
			MediaProvider.Mdblist,
			MediaProvider.Tmdb,
			MediaProvider.Imdb,
			MediaProvider.Tvdb,
			MediaProvider.Plex,
		];

		var tiles = ServicesMapModel.PlaceTiles(sources, 0, 10);

		await Assert
			.That(tiles.Select(tile => $"{tile.Provider} {tile.X},{Math.Round(tile.Y, 2)} {tile.Entry}"))
			.IsEquivalentTo([
				"Tmdb 596,0 Bottom",
				"Imdb 629,0 Bottom",
				"Tvdb 662,0 Bottom",
				"Plex 695,0 Bottom",
				"Mdblist 728,0 Bottom",
				"Tmdb 596,82.2 Top",
				"Imdb 629,82.2 Top",
				"Tvdb 662,82.2 Top",
				"Plex 695,82.2 Top",
			]);
	}

	[Test]
	public async Task PlaceTiles_PastSixteenSources_GrowsLeftOfTheServiceColumn()
	{
		var tiles = ServicesMapModel.PlaceTiles([.. Enumerable.Repeat(MediaProvider.Mdblist, 17)], 0, 18);

		await Assert.That(tiles[0].X).IsEqualTo(570);
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
		var runs = ServicesMapModel
			.For(Config, [MediaProvider.Tmdb, .. Enumerable.Repeat(MediaProvider.Mdblist, sources)])
			.Traces.Select(trace => trace.Points.Zip(trace.Points.Skip(1)).ToList())
			.ToList();

		var crossings =
			from first in Enumerable.Range(0, runs.Count)
			from second in Enumerable.Range(first + 1, runs.Count - first - 1)
			from a in runs[first]
			from b in runs[second]
			where Crosses(a, b) || Crosses(b, a)
			select (first, second);
		await Assert.That(crossings).IsEmpty();
	}

	[Test]
	[Arguments(0, 0, 20, 0, 20, 20, "M0 0 L14 0 L20 6 L20 20")]
	[Arguments(0, 0, 10, 0, 10, 4, "M0 0 L8 0 L10 2 L10 4")]
	[Arguments(0, 0, 10, 0, 10, 3.3, "M0 0 L8.35 0 L10 1.65 L10 3.3")]
	public async Task Path_ForAnLShapedRun_CutsTheCornerAt45Degrees(double x0, double y0, double x1, double y1, double x2, double y2, string path)
	{
		var trace = new MapTrace("Feeds", MediaProvider.Tmdb, [new(x0, y0), new(x1, y1), new(x2, y2)]);

		await Assert.That(trace.Path).IsEqualTo(path);
	}

	[Test]
	public async Task Path_UnderACommaDecimalCulture_StillWritesDots()
	{
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
		var trace = new MapTrace("Feeds", MediaProvider.Tmdb, [new(0, 0.5), new(10.25, 0.5)]);

		await Assert.That(trace.Path).IsEqualTo("M0 0.5 L10.25 0.5");
	}

	[Test]
	public async Task Path_ForARepeatedPoint_DrawsItOnce()
	{
		var trace = new MapTrace("Wantlist", MediaProvider.Plex, [new(0, 0), new(10, 0), new(10, 0), new(20, 0)]);

		await Assert.That(trace.Path).IsEqualTo("M0 0 L5 0 L15 0 L20 0");
	}

	private static bool Crosses((MapPoint First, MapPoint Second) horizontal, (MapPoint First, MapPoint Second) vertical) =>
		horizontal.First.Y == horizontal.Second.Y
		&& vertical.First.X == vertical.Second.X
		&& Between(horizontal.First.Y, vertical.First.Y, vertical.Second.Y)
		&& Between(vertical.First.X, horizontal.First.X, horizontal.Second.X);

	private static bool Between(double value, double from, double to) => value > Math.Min(from, to) && value < Math.Max(from, to);
}
