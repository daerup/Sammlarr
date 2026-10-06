using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.SelfHosted;

[SelfHosted]
public class SinkFailureTests
{
	[Test]
	public async Task SyncPreview_WhileSonarrRefusesItsKey_ShowsSonarrsReasonAndBlocksTheSync()
	{
		var stack = await SelfHostedApp.StackAsync();
		var page = await SelfHostedApp.OpenPageAsync();
		await SelfHostedSetup.FinishAsync(page, stack);
		await SelfHostedWantlist.AddAsync(page, Titles.ProjectHailMary);

		await stack.MakeSonarrRefuseItsKeyAsync();
		var preview = await SelfHostedWantlist.OpenSyncAsync(page);

		await Expect(preview.GetByText("Sonarr answered 401 Unauthorized")).ToBeVisibleAsync(Wait.Settle);
		await Expect(preview.ButtonContaining("Sync now")).ToBeDisabledAsync();
		await page.Keyboard.PressAsync("Escape");
		await Expect(page.Locator(".sync-app[data-app=Sonarr]")).ToContainTextAsync("unreachable");
		await Expect(page.Locator(".sync-app[data-app=Radarr]")).Not.ToContainTextAsync("unreachable");
	}
}
