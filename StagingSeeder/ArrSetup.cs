namespace StagingSeeder;

internal sealed record StagedApp(int[] Categories)
{
	internal static readonly StagedApp Radarr = new([2000, 2040]);

	internal static readonly StagedApp Sonarr = new([5000, 5040]);
}

internal static class ArrSetup
{
	private const string Profile = "HD-1080p";

	internal static async Task<int> PrepareAsync(ArrClient arr, StagedApp app, ArrAppOptions options, string indexerUrl, CancellationToken cancellationToken)
	{
		await arr.WaitUntilUpAsync(cancellationToken);
		if (await arr.IsEmptyAsync("rootfolder", cancellationToken))
		{
			await arr.PostAsync("rootfolder", new { path = options.RootFolder }, cancellationToken);
		}

		if (await arr.IsEmptyAsync("downloadclient", cancellationToken))
		{
			await arr.PostAsync("downloadclient?forceSave=true", Blackhole(), cancellationToken);
		}

		if (await arr.IsEmptyAsync("indexer", cancellationToken))
		{
			await arr.PostAsync("indexer?forceSave=true", Indexer(app, indexerUrl), cancellationToken);
		}

		return await arr.IdOfAsync("qualityprofile", "name", Profile, cancellationToken);
	}

	private static object Blackhole()
	{
		return new
		{
			name = "Staging downloader",
			implementation = "UsenetBlackhole",
			configContract = "UsenetBlackholeSettings",
			protocol = "usenet",
			enable = true,
			priority = 1,
			tags = Array.Empty<int>(),
			fields = new Field[] { new("nzbFolder", "/downloads/nzb"), new("watchFolder", "/downloads/watch") },
		};
	}

	private static object Indexer(StagedApp app, string indexerUrl)
	{
		return new
		{
			name = "Staging indexer",
			implementation = "Newznab",
			configContract = "NewznabSettings",
			protocol = "usenet",
			enableRss = true,
			enableAutomaticSearch = true,
			enableInteractiveSearch = true,
			priority = 25,
			tags = Array.Empty<int>(),
			fields = new Field[] { new("baseUrl", indexerUrl), new("apiPath", "/api"), new("apiKey", "staging"), new("categories", app.Categories) },
		};
	}

	private sealed record Field(string Name, object Value);
}
