using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;
using Web.Staging;

namespace Integration.Tests.Staging;

public class StagingRunTests
{
	[Test]
	public async Task RunAsync_WithARelativeDatabase_RefusesBeforeDeletingOrSeedingAnything()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Sammlarr:ConnectionString"] = "Data Source=sammlarr.db" })
			.Build();
		var run = new StagingRun(new ServiceCollection().BuildServiceProvider(), configuration, NullLogger<StagingRun>.Instance);

		await Assert
			.That(async () => await run.RunAsync(CancellationToken.None))
			.Throws<InvalidOperationException>()
			.WithMessageContaining("absolute path only, not 'sammlarr.db'");
	}

	[Test]
	public async Task Settings_ConnectsEachAppAtItsAddressAndLinksToWhereTheHostReachesIt()
	{
		var options = new StagingOptions
		{
			Scenario = StagingScenario.Configured,
			Personas = new StagingPersonas
			{
				Admin = new StagingPersona { Name = "admin", Password = "admin" },
				Member = new StagingPersona { Name = "member", Password = "member" },
			},
			Seed = new StagingSeedData
			{
				Wantlist = [],
				Feeds = [],
				Library = new SeedLibrary { Radarr = [], Sonarr = [] },
				Notifications = new SeedNotifications
				{
					Webhook = new SeedWebhook { Name = "Catcher webhook", Secret = "whsec_staging" },
					Pushover = new SeedPushover
					{
						Name = "Catcher Pushover",
						AppToken = "staging-app-token",
						UserKey = "staging-user-key",
					},
				},
			},
			TmdbKey = "staging-tmdb-key",
			ArrApiKey = "0123456789abcdef0123456789abcdef",
			IndexerUrl = "http://wiremock:8080/newznab",
			CatcherUrl = "http://catcher:8080/session",
			Radarr = new ArrAppOptions
			{
				Url = "http://localhost:7879",
				Address = "http://radarr:7878",
				RootFolder = "/movies",
				Library = "movies",
			},
			Sonarr = new ArrAppOptions
			{
				Url = "http://localhost:8990",
				Address = "http://sonarr:8989",
				RootFolder = "/tv",
				Library = "tv",
			},
		};

		var arr = StagingRun.Settings(options, movies: 4, shows: 6).Arr;

		await Assert.That(arr.Radarr.Url).IsSome().And.IsEqualTo("http://radarr:7878");
		await Assert.That(arr.Radarr.ExternalUrl).IsSome().And.IsEqualTo("http://localhost:7879");
		await Assert.That(arr.Sonarr.Url).IsSome().And.IsEqualTo("http://sonarr:8989");
		await Assert.That(arr.Sonarr.ExternalUrl).IsSome().And.IsEqualTo("http://localhost:8990");
	}
}
