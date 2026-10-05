using System.Text.Json.Nodes;
using API.Arr;
using API.Arr.Radarr;
using API.Arr.Types;
using Contracts.Settings;
using Domain.Media;
using Shared.Options;

namespace API.Tests.Arr.Radarr;

public class RadarrApiTests
{
	private static readonly ArrTitle Matrix = new(10, "The Matrix", 1999, 603, 0, "tt0133093", []);

	private static readonly ArrAddition Addition = new(4, "/movies", [1, 2]);

	[Test]
	public async Task IdsOf_AMovie_IsItsTmdbId()
	{
		IExternalMediaId[] tmdb = [TmdbId.From("603", MediaType.Movie)];

		await Assert.That(Testee().IdsOf(Matrix)).IsEquivalentTo(tmdb);
	}

	[Test]
	public async Task AddressOf_AMovie_IsItsTmdbId()
	{
		await Assert.That(Testee().AddressOf(Matrix)).IsEqualTo(603);
	}

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
