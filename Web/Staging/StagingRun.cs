using Domain.Wantlist.Arr;
using Microsoft.Data.Sqlite;
using Shared.Options;

namespace Web.Staging;

internal sealed record StagingOptions
{
	internal const string SectionName = "Staging";

	public required StagingScenario Scenario { get; init; }

	public required string TmdbKey { get; init; }

	public string? MdblistKey { get; init; }

	public required string ArrApiKey { get; init; }

	public required string IndexerUrl { get; init; }

	public required string CatcherUrl { get; init; }

	public required ArrAppOptions Radarr { get; init; }

	public required ArrAppOptions Sonarr { get; init; }
}

internal sealed record ArrAppOptions
{
	public required string Url { get; init; }

	public required string Address { get; init; }

	public required string RootFolder { get; init; }

	public required string Library { get; init; }
}

internal sealed class StagingRun(IServiceProvider services, IConfiguration configuration, ILogger<StagingRun> logger)
{
	internal async Task RunAsync(CancellationToken cancellationToken)
	{
		var database = Database();
		var options = configuration.GetRequiredSection(StagingOptions.SectionName).Get<StagingOptions>()!;
		var radarr = ArrClient.For(options.Radarr.Url, options.ArrApiKey);
		var sonarr = ArrClient.For(options.Sonarr.Url, options.ArrApiKey);
		var movies = await ArrSetup.PrepareAsync(radarr, StagedApp.Radarr, options.Radarr, options.IndexerUrl, cancellationToken);
		var shows = await ArrSetup.PrepareAsync(sonarr, StagedApp.Sonarr, options.Sonarr, options.IndexerUrl, cancellationToken);
		await StagingLibrary.FillRadarrAsync(radarr, options.Radarr, movies, cancellationToken);
		await StagingLibrary.FillSonarrAsync(sonarr, options.Sonarr, shows, cancellationToken);

		var seeded = $"{database}.seeded";
		if (File.Exists(seeded))
		{
			logger.LogInformation("Kept the staging database at {Database}", database);
			return;
		}

		foreach (var leftover in new[] { database, $"{database}-wal", $"{database}-shm" }.Where(File.Exists))
		{
			File.Delete(leftover);
		}

		await StagingSeed.SeedAsync(services, options.Scenario, Settings(options, movies, shows), cancellationToken);
		await File.WriteAllTextAsync(seeded, options.Scenario.ToString(), cancellationToken);
		logger.LogInformation("Seeded the {Scenario} scenario into {Database}", options.Scenario, database);
		if (options.Scenario == StagingScenario.Configured)
		{
			logger.LogInformation(
				"Sign in as {Admin} / {AdminPassword} (admin) or {Member} / {MemberPassword} (member)",
				StagingPersonas.Admin,
				StagingPersonas.AdminPassword,
				StagingPersonas.Member,
				StagingPersonas.MemberPassword
			);
		}
	}

	private string Database()
	{
		var database = new SqliteConnectionStringBuilder(configuration.GetSection(SammlarOptions.SectionName)[nameof(SammlarOptions.ConnectionString)]).DataSource;
		return Path.IsPathRooted(database)
			? database
			: throw new InvalidOperationException($"The staging seed deletes and reseeds its database, so it takes an absolute path only, not '{database}'.");
	}

	private static StagingSettings Settings(StagingOptions options, int movies, int shows)
	{
		var radarr = Instance(options.Radarr, options.ArrApiKey, movies);
		var sonarr = Instance(options.Sonarr, options.ArrApiKey, shows);
		return new StagingSettings(
			options.TmdbKey,
			Optional.Of(options.MdblistKey),
			ArrSinkConfig.Empty with
			{
				Radarr = radarr,
				Sonarr = sonarr,
			},
			options.CatcherUrl
		);
	}

	private static ArrInstanceConfig Instance(ArrAppOptions app, string apiKey, int profileId)
	{
		return new ArrInstanceConfig
		{
			Url = Some.Of(app.Address),
			ApiKey = Some.Of(apiKey),
			QualityProfileId = Some.Of(profileId),
			RootFolderPath = Some.Of(app.RootFolder),
		};
	}
}
