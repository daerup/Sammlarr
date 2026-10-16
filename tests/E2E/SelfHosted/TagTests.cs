using E2E.Tests.Settings;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class TagTests
{
	private const string ProtectedTag = "sammlarr-protected";

	[Test]
	public async Task Movie_AlreadyInRadarrWhenWanted_IsProtectedAndOnlyUntaggedWhenTakenOffTheWantlist()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await stack.Radarr.AddMovieAsync(Titles.ProjectHailMaryTmdb);

		await SelfHostedWantlist.AddAsync(page, Titles.ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);
		var wanted = await stack.Radarr.WaitForMovieAsync(Titles.ProjectHailMaryTmdb);
		var sammlarrTags = new[] { await stack.Radarr.TagIdAsync(Titles.AdminTag), await stack.Radarr.TagIdAsync(ProtectedTag) };
		await SelfHostedWantlist.RemoveAsync(page, Titles.ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);

		var left = await stack.Radarr.WaitForMovieAsync(Titles.ProjectHailMaryTmdb);
		await Assert.That(ArrApi.TagsOf(wanted)).IsEquivalentTo(sammlarrTags);
		await Assert.That(ArrApi.TagsOf(left)).IsEmpty();
	}

	[Test]
	public async Task Prefix_ChangedThroughItsDialog_TagsTheNextSyncedTitleWithIt()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SettingsPage.OpenWantlistSyncAsync(page);
		await page.GetByRole(AriaRole.Tab).Filter(new LocatorFilterOptions { HasText = "Tags" }).ClickAsync();

		await page.Locator("button[aria-label='Change the prefix']").ClickAsync();
		await page.Locator("#arr-tag-change").EnterAsync("media");
		await page.Button("Change prefix").ClickAsync();
		await Expect(page.Locator("[data-locked=prefix]")).ToContainTextAsync("media");
		await SelfHostedWantlist.AddAsync(page, Titles.ProjectHailMary);
		await SelfHostedWantlist.SyncAsync(page);

		var movie = await stack.Radarr.WaitForMovieAsync(Titles.ProjectHailMaryTmdb);
		await Assert.That(ArrApi.TagsOf(movie)).Contains(await stack.Radarr.TagIdAsync($"media-u-{SelfHostedSetup.AdminName}"));
	}
}
