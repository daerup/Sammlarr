using E2E.Tests.Settings;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class SyncTests
{
	private const string Profile = "HD-1080p";

	[Test]
	public async Task Movie_FoundOnTmdbAndSynced_IsCountedAsPushedAndReachesRadarrAsSetUpTaggedAndSearched()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SettingsPage.OpenWantlistSyncAsync(page);
		var radarr = await ArrSections.OpenAsync(page, "Radarr");
		var profiles = radarr.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = "Radarr quality profile" });
		await profiles.ClickAsync();
		await page.Option(Profile).ClickAsync();
		await Expect(profiles).ToContainTextAsync(Profile);

		await SelfHostedWantlist.AddAsync(page, Titles.ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);

		await Expect(page.GetByText("Pulled 0, pushed 1.")).ToBeVisibleAsync();
		var movie = await stack.Radarr.WaitForMovieAsync(Titles.ProjectHailMaryTmdb);
		var profile = (await stack.Radarr.GetAsync("qualityprofile")).AsArray().Single(found => found!["name"]!.GetValue<string>() == Profile)!;
		await Assert.That(movie["qualityProfileId"]!.GetValue<int>()).IsEqualTo(profile["id"]!.GetValue<int>());
		await Assert.That(movie["rootFolderPath"]!.GetValue<string>()).IsEqualTo("/movies");
		await Assert.That(movie["monitored"]!.GetValue<bool>()).IsTrue();
		await Assert.That(ArrApi.TagsOf(movie)).Contains(await stack.Radarr.TagIdAsync(Titles.AdminTag));
		await stack.WaitForIndexerSearchAsync($"tmdbid={Titles.ProjectHailMaryTmdb}");
	}

	[Test]
	public async Task Movie_WithNoSearch_ReachesRadarrWithoutASearch()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SettingsPage.OpenWantlistSyncAsync(page);
		var radarr = await ArrSections.OpenAsync(page, "Radarr");
		var noSearch = radarr
			.GetByRole(AriaRole.Radiogroup, new LocatorGetByRoleOptions { Name = "Radarr starts a search when adding" })
			.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "No search" });
		await noSearch.ClickAsync();
		await Expect(noSearch).ToHaveAttributeAsync("aria-checked", "true");

		await SelfHostedWantlist.AddAsync(page, Titles.ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);

		var movie = await stack.Radarr.WaitForMovieAsync(Titles.ProjectHailMaryTmdb);
		await stack.Radarr.WaitForRefreshAsync(movie);
		await stack.Radarr.SettleAsync();
		var searches = await stack.IndexerSearchesAsync();
		await Assert.That(searches.Where(url => new[] { "q=", "tmdbid=", "imdbid=" }.Any(term => url.Contains(term, StringComparison.Ordinal)))).IsEmpty();
	}

	[Test]
	public async Task Show_SonarrTagsAnime_ReachesSonarrsAnimeFolderAsAnAnimeSeries()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SettingsPage.OpenWantlistSyncAsync(page);
		var sonarr = await ArrSections.OpenAsync(page, "Sonarr");
		await sonarr.ButtonContaining("Anime folder and profile").ClickAsync();
		var folders = sonarr.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = "Sonarr anime root folder" });
		await folders.ClickAsync();
		await page.OptionContaining("/anime").ClickAsync();
		await Expect(folders).ToContainTextAsync("/anime");
		await SelfHostedWantlist.SearchWithRadarrAndSonarrAsync(page);

		await SelfHostedWantlist.AddAsync(page, Titles.Frieren);
		await SelfHostedWantlist.SyncAsync(page);

		var series = await stack.Sonarr.WaitForSeriesAsync(Titles.FrierenTvdb);
		await Assert.That(series["rootFolderPath"]!.GetValue<string>()).IsEqualTo("/anime");
		await Assert.That(series["seriesType"]!.GetValue<string>()).IsEqualTo("anime");
	}

	[Test]
	public async Task Show_FoundThroughRadarrAndSonarrAndSynced_ReachesSonarrTaggedAndIsSearchedBySeason()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SelfHostedWantlist.SearchWithRadarrAndSonarrAsync(page);

		await SelfHostedWantlist.AddAsync(page, Titles.BreakingBad);
		await SelfHostedWantlist.SyncAsync(page);

		var series = await stack.Sonarr.WaitForSeriesAsync(Titles.BreakingBadTvdb);
		await Assert.That(ArrApi.TagsOf(series)).Contains(await stack.Sonarr.TagIdAsync(Titles.AdminTag));
		foreach (var season in Enumerable.Range(1, 5))
		{
			await stack.WaitForIndexerSearchAsync($"tvdbid={Titles.BreakingBadTvdb}", $"season={season}");
		}
	}
}
