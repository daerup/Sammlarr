using System.Text.Json.Nodes;
using E2E.Tests.Settings;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class SyncTests
{
	private const string ProjectHailMary = "Project Hail Mary";

	private const int ProjectHailMaryTmdb = 687163;

	private const string BreakingBad = "Breaking Bad";

	private const int BreakingBadTvdb = 81189;

	private const string Profile = "HD-1080p";

	private static readonly string AdminTag = $"sammlarr-u-{SelfHostedSetup.AdminName}";

	[Test]
	public async Task Movie_FoundOnTmdbAndSynced_ReachesRadarrAsSetUpTaggedForItsPersonAndIsSearched()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		var settings = await SettingsPage.OpenWantlistSyncAsync(page);
		var profiles = settings.GetByRole(AriaRole.Combobox, new LocatorGetByRoleOptions { Name = "Radarr quality profile" });
		await profiles.ClickAsync();
		await page.Option(Profile).ClickAsync();
		await Expect(profiles).ToContainTextAsync(Profile);

		await SelfHostedWantlist.AddAsync(page, ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);

		var movie = await stack.Radarr.WaitForAsync("movie", item => item["tmdbId"]!.GetValue<int>() == ProjectHailMaryTmdb);
		var profile = (await stack.Radarr.GetAsync("qualityprofile")).AsArray().Single(found => found!["name"]!.GetValue<string>() == Profile)!;
		await Assert.That(movie["qualityProfileId"]!.GetValue<int>()).IsEqualTo(profile["id"]!.GetValue<int>());
		await Assert.That(movie["rootFolderPath"]!.GetValue<string>()).IsEqualTo("/movies");
		await Assert.That(movie["monitored"]!.GetValue<bool>()).IsTrue();
		await Assert.That(Tags(movie)).Contains(await TagIdAsync(stack.Radarr, AdminTag));
		await stack.WaitForIndexerSearchAsync($"tmdbid={ProjectHailMaryTmdb}");
	}

	[Test]
	public async Task Movie_WithSearchWhenAddedOff_ReachesRadarrWithoutASearch()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		var settings = await SettingsPage.OpenWantlistSyncAsync(page);
		var searchWhenAdded = settings.Locator("button[role=switch][aria-label='Radarr searches when a title is added']");
		await searchWhenAdded.ClickAsync();
		await Expect(searchWhenAdded).ToHaveAttributeAsync("aria-checked", "false");

		await SelfHostedWantlist.AddAsync(page, ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);

		var movie = await stack.Radarr.WaitForAsync("movie", item => item["tmdbId"]!.GetValue<int>() == ProjectHailMaryTmdb);
		await stack.Radarr.WaitForRefreshAsync(movie);
		await stack.Radarr.SettleAsync();
		var searches = await stack.IndexerSearchesAsync();
		await Assert.That(searches.Where(url => new[] { "q=", "tmdbid=", "imdbid=" }.Any(term => url.Contains(term, StringComparison.Ordinal)))).IsEmpty();
	}

	[Test]
	public async Task Show_FoundThroughRadarrAndSonarrAndSynced_ReachesSonarrTaggedAndIsSearchedBySeason()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SettingsPage.PickAsync(page, "Search", "Radarr + Sonarr");
		await Expect(page.GetByText("Search now uses Radarr + Sonarr.")).ToBeVisibleAsync();

		await SelfHostedWantlist.AddAsync(page, BreakingBad);
		await SelfHostedWantlist.SyncAsync(page);

		var series = await stack.Sonarr.WaitForAsync("series", item => item["tvdbId"]!.GetValue<int>() == BreakingBadTvdb);
		await Assert.That(Tags(series)).Contains(await TagIdAsync(stack.Sonarr, AdminTag));
		foreach (var season in Enumerable.Range(1, 5))
		{
			await stack.WaitForIndexerSearchAsync($"tvdbid={BreakingBadTvdb}", $"season={season}");
		}
	}

	private static IEnumerable<int> Tags(JsonNode title) => title["tags"]!.AsArray().Select(id => id!.GetValue<int>());

	private static async Task<int> TagIdAsync(ArrApi arr, string label)
	{
		var tag = (await arr.GetAsync("tag")).AsArray().Single(found => found!["label"]!.GetValue<string>() == label)!;
		return tag["id"]!.GetValue<int>();
	}
}
