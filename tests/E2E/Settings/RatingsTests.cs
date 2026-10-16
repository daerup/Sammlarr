using Microsoft.Playwright;
using TUnit.Assertions.Enums;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class RatingsTests
{
	[Test]
	public async Task Average_WhenAProviderIsSwitchedOff_LeavesEveryTileWhereItWas()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("ratings"));
		var tiles = page.Locator(".pipeline-tile");
		await Expect(tiles.First).ToBeVisibleAsync();
		var before = await BoxesAsync(tiles);

		var rottenTomatoes = page.GetByRole(AriaRole.Switch, new PageGetByRoleOptions { Name = "Rotten Tomatoes® critics counts" });
		await rottenTomatoes.ClickAsync();

		await Expect(rottenTomatoes).ToHaveAttributeAsync("aria-checked", "false");
		await Assert.That(await BoxesAsync(tiles)).IsEquivalentTo(before, CollectionOrdering.Matching);
	}

	private static async Task<List<(float X, float Y)>> BoxesAsync(ILocator tiles)
	{
		var boxes = new List<(float X, float Y)>();
		foreach (var tile in await tiles.AllAsync())
		{
			var box = await tile.BoundingBoxAsync();
			boxes.Add((box!.X, box.Y));
		}

		return boxes;
	}
}
