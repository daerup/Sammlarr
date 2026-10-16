using System.Text.Json.Nodes;
using API.Arr;
using API.Arr.Radarr;
using Domain.Wantlist.Arr;
using Shared.Options;

namespace API.Tests.Arr.Radarr;

public class RadarrApiTests
{
	private static readonly ArrAddition Addition = new(4, "/movies", [1, 2]);

	[Test]
	public Task Prepare_WithTheDefaultOptions_AddsAReleasedMonitoredMovie()
	{
		return VerifyJson(Testee().Prepare(Lookup(), ArrInstanceConfig.Empty, Addition).ToJsonString());
	}

	[Test]
	public Task Prepare_WhenInCinemasAndNotMonitored_AddsItWithoutSearching()
	{
		var radarr = Testee(new RadarrOptions { MinimumAvailability = RadarrAvailability.InCinemas });

		return VerifyJson(radarr.Prepare(Lookup(), ArrInstanceConfig.Empty with { Monitored = false, SearchOnAdd = false }, Addition).ToJsonString());
	}

	private static RadarrApi Testee() => Testee(new RadarrOptions());

	private static RadarrApi Testee(RadarrOptions options) => new(new HttpClient(), options);

	private static JsonObject Lookup() => new() { ["title"] = "The Matrix", ["tmdbId"] = 603 };
}
