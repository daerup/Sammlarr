using API.Http.Lanes;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Options;

namespace API.Tests.Http;

public class LanesTests
{
	private static readonly Dictionary<string, LaneOptions> PlexLanes = new()
	{
		["api"] = new LaneOptions(),
		["match"] = new LaneOptions { Routes = [new LaneRouteOptions { Url = "discover", Path = "library/metadata/matches" }] },
		["metadata"] = new LaneOptions { Routes = [new LaneRouteOptions { Url = "metadata", Path = "library/metadata" }] },
		["watchlist"] = new LaneOptions
		{
			Routes = [new LaneRouteOptions { Url = "discover", Path = "library/sections/watchlist" }, new LaneRouteOptions { Url = "vod", Path = "actions" }],
		},
	};

	[Test]
	public async Task Route_ForTwoMatchingPaths_PicksTheLongest()
	{
		var lanes = new Dictionary<string, LaneOptions>
		{
			["library"] = new() { Routes = [new LaneRouteOptions { Url = "plex", Path = "library" }] },
			["sections"] = new() { Routes = [new LaneRouteOptions { Url = "plex", Path = "library/sections" }] },
		};

		await Assert
			.That(Testee(LanedHosts.Plex(new ProviderHttpOptions { Lanes = lanes })).Route(RemoteService.Plex, new Uri("https://plex.test/library/sections/1")))
			.IsEqualTo("sections");
	}

	[Test]
	public async Task Route_ForPlexMetadataOnTheConfiguredMetadataUrl_PicksTheMetadataRoute()
	{
		var route = Testee(Shipped())
			.Route(RemoteService.Plex, new Uri("https://metadata.provider.plex.tv/library/metadata/5d776be17a53e9001e732ab9?includeGuids=1"));

		await Assert.That(route).IsEqualTo("metadata");
	}

	[Test]
	public async Task Route_ForAPlexMatchOnTheDiscoverUrl_PicksTheMatchLane()
	{
		var route = Testee(Shipped()).Route(RemoteService.Plex, new Uri("https://discover.provider.plex.tv/library/metadata/matches?type=1&guid=tmdb%3A%2F%2F550"));

		await Assert.That(route).IsEqualTo("match");
	}

	[Test]
	public async Task Route_ForAVodAddToWatchlistCall_PicksTheWatchlistLane()
	{
		var route = Testee(Shipped()).Route(RemoteService.Plex, new Uri("https://vod.provider.plex.tv/actions/addToWatchlist?ratingKey=5d776be17a53e9001e732ab9"));

		await Assert.That(route).IsEqualTo("watchlist");
	}

	[Test]
	public async Task Route_ForAbsoluteTmdbUrlsWithoutABaseAddress_PicksTheLaneOfEachConfiguredUrl()
	{
		var tmdb = new TmdbOptions
		{
			BaseUrl = "https://api.themoviedb.org/3",
			V4BaseUrl = "https://api.themoviedb.org/4",
			ImageBaseUrl = "https://image.tmdb.org/t/p",
			Http = new ProviderHttpOptions
			{
				Lanes = new()
				{
					["v3"] = new LaneOptions { Routes = [new LaneRouteOptions { Url = "api" }] },
					["v4"] = new LaneOptions { Routes = [new LaneRouteOptions { Url = "v4" }] },
				},
			},
		};
		using var lanes = new Lanes(TimeProvider.System, [new RemoteSettings(RemoteService.Tmdb, tmdb)], new ServiceCollection().BuildServiceProvider());

		await Assert.That(lanes.Route(RemoteService.Tmdb, new Uri("https://api.themoviedb.org/3/movie/550?append_to_response=external_ids"))).IsEqualTo("v3");
		await Assert.That(lanes.Route(RemoteService.Tmdb, new Uri("https://api.themoviedb.org/4/list/8136"))).IsEqualTo("v4");
	}

	[Test]
	public async Task Route_OnStagingUrls_MatchesTheSameRoutes()
	{
		var staging = Shipped() with
		{
			DiscoverUrl = "http://localhost:9090/plex-discover",
			MetadataUrl = "http://localhost:9090/plex-metadata",
			VodUrl = "http://localhost:9090/plex-vod",
		};
		var testee = Testee(staging);

		await Assert.That(testee.Route(RemoteService.Plex, new Uri("http://localhost:9090/plex-metadata/library/metadata/1"))).IsEqualTo("metadata");
		await Assert.That(testee.Route(RemoteService.Plex, new Uri("http://localhost:9090/plex-discover/library/sections/watchlist/all"))).IsEqualTo("watchlist");
		await Assert
			.That(testee.Route(RemoteService.Plex, new Uri("http://localhost:9090/plex-discover/library/metadata/matches?type=1&guid=tmdb%3A%2F%2F550")))
			.IsEqualTo("match");
		await Assert.That(testee.Route(RemoteService.Plex, new Uri("http://localhost:9090/plex-discover/library/search?query=heat"))).IsEqualTo(Lanes.Rest);
	}

	[Test]
	public async Task Check_ForALaneRoutedToAUrlTheProviderDoesNotHave_RefusesToStart()
	{
		var http = new ProviderHttpOptions
		{
			Lanes = new() { ["match"] = new LaneOptions { Routes = [new LaneRouteOptions { Url = "matches", Path = "library" }] } },
		};
		var check = new LaneSettingsCheck([new RemoteSettings(RemoteService.Plex, LanedHosts.Plex(http))]);

		await Assert.That(async () => await check.StartAsync(CancellationToken.None)).Throws<InvalidOperationException>();
	}

	private static PlexOptions Shipped()
	{
		return LanedHosts.Plex(new ProviderHttpOptions { Lanes = PlexLanes }) with
		{
			BaseUrl = "https://plex.tv",
			DiscoverUrl = "https://discover.provider.plex.tv",
			MetadataUrl = "https://metadata.provider.plex.tv",
			VodUrl = "https://vod.provider.plex.tv",
		};
	}

	private static Lanes Testee(PlexOptions options)
	{
		return new Lanes(TimeProvider.System, [new RemoteSettings(RemoteService.Plex, options)], new ServiceCollection().BuildServiceProvider());
	}
}
