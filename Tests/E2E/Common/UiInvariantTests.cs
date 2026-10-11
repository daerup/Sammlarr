using System.Text.RegularExpressions;
using E2E.Tests.Settings;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Common;

public class UiInvariantTests
{
	private const string NameTooltipTrigger = "[id^=\"tooltip-\"][id$=\"-trigger\"]:has(.pipeline-name)";
	private const string OpenTooltip = "[role=tooltip][data-state=open]";

	[Test]
	public async Task Tooltip_WhenItsTriggerTakesFocusWithoutTab_StaysClosed()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("ratings"));
		var trigger = page.Locator(NameTooltipTrigger).First;
		await Expect(trigger).ToBeVisibleAsync(Wait.Settle);

		await trigger.FocusAsync();
		await Expect(trigger).ToBeFocusedAsync();
		await page.WaitForTimeoutAsync(600);

		await Expect(page.Locator(OpenTooltip)).ToHaveCountAsync(0);
	}

	[Test]
	public async Task Tooltip_WhenTabbedTo_Opens()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("ratings"));
		var trigger = page.Locator(NameTooltipTrigger).First;
		await Expect(trigger).ToBeVisibleAsync(Wait.Settle);

		await trigger.FocusAsync();
		await page.Keyboard.PressAsync("Shift+Tab");
		await page.Keyboard.PressAsync("Tab");

		await Expect(trigger).ToBeFocusedAsync();
		await Expect(trigger).ToHaveAttributeAsync("aria-describedby", new Regex("."), new LocatorAssertionsToHaveAttributeOptions { Timeout = Wait.Settle });
		await Expect(page.Locator($"#{await trigger.GetAttributeAsync("aria-describedby")}")).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task Tooltip_WhenItsCloseWasAbandoned_Hides()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("ratings"));
		var trigger = page.Locator(NameTooltipTrigger).First;
		await Expect(trigger).ToBeVisibleAsync(Wait.Settle);
		await trigger.HoverAsync();
		var portal = page.Locator($"[data-bb-portal]:has({OpenTooltip})");
		await Expect(portal).ToBeVisibleAsync(Wait.Settle);

		await portal.EvaluateAsync(
			"""
			portal => {
				portal.style.setProperty('pointer-events', 'none', 'important');
				portal.style.setProperty('visibility', 'visible', 'important');
				portal.dataset.state = 'closed';
			}
			"""
		);
		await Expect(page.Locator(OpenTooltip)).ToHaveCountAsync(1);

		await Expect(portal).ToBeHiddenAsync(1500);
	}

	[Test]
	public async Task CtrlK_WhenPressed_OpensSearch()
	{
		var page = await E2EApp.AdminAt(Routes.Blocklist);
		await Expect(page.Heading("Blocklist")).ToBeVisibleAsync(Wait.Settle);
		var search = page.Modal("Search");
		await Expect(search).ToBeHiddenAsync();

		for (var attempt = 0; attempt < 10 && !await search.IsVisibleAsync(); attempt++)
		{
			await page.Keyboard.PressAsync("Control+k");
			await page.WaitForTimeoutAsync(1000);
		}

		await Expect(search).ToBeVisibleAsync(Wait.Settle);
	}

#if DEBUG
	[Test]
	public async Task TestingSection_WhenExceptionThrown_OpensErrorDialog()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("testing"));

		var toasts = page.Locator("section").Filter(new LocatorFilterOptions { HasText = "Fire a toast of the chosen kind" });
		await PressAsync(toasts.Combobox);
		await PressAsync(page.Option("Exception"));
		await PressAsync(toasts.Button("Show"));
		await page.Button("View details").ClickAsync();

		await Expect(page.Locator(".error-dialog")).ToBeVisibleAsync(Wait.Settle);
		await Expect(page.GetByText("System.InvalidOperationException")).ToBeVisibleAsync();
		await Expect(page.GetByText("Stack Trace")).ToBeVisibleAsync();
	}

	[Test]
	public async Task UnmatchedTile_InTheLightTheme_KeepsItsTitleReadable()
	{
		var page = await E2EApp.AdminAt(SettingsPage.Section("testing"));
		await Expect(page.Locator("html")).Not.ToHaveClassAsync(new Regex("dark"));
		var tile = page.Locator("[data-testid=media-tile]", new PageLocatorOptions { HasText = "Unmatched Title" });
		await Expect(tile).ToBeVisibleAsync(Wait.Settle);

		var contrast = await tile.EvaluateAsync<double>(
			"""
			tile => {
				const rgb = color => {
					const canvas = document.createElement("canvas");
					canvas.width = canvas.height = 1;
					const context = canvas.getContext("2d");
					context.fillStyle = color;
					context.fillRect(0, 0, 1, 1);
					return [...context.getImageData(0, 0, 1, 1).data].slice(0, 3);
				};
				const luminance = color => rgb(color)
					.map(value => value / 255)
					.map(value => value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4)
					.reduce((sum, value, index) => sum + value * [0.2126, 0.7152, 0.0722][index], 0);
				const placeholder = tile.querySelector(".media-card-failed");
				const title = tile.querySelector("h3");
				const layers = [];
				for (let element = placeholder; element !== tile.parentElement; element = element.parentElement) layers.push(element);
				const covered = [...layers, title].some(element => getComputedStyle(element).filter !== "none" || getComputedStyle(element).opacity !== "1")
					|| getComputedStyle(tile.querySelector(".media-card-scrim")).display !== "none";
				const ink = luminance(getComputedStyle(title).color);
				const ground = luminance(getComputedStyle(placeholder).backgroundColor);
				return covered ? 0 : (Math.max(ink, ground) + 0.05) / (Math.min(ink, ground) + 0.05);
			}
			"""
		);

		await Assert.That(contrast).IsGreaterThanOrEqualTo(4.5);
	}
#endif

	[Test]
	public async Task Fonts_WhenAPageLoads_ComeFromTheAppInsteadOfGoogle()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		var requested = new List<string>();
		page.Request += (_, request) => requested.Add(request.Url);

		await page.VisitAsync(Routes.Feeds);
		await Expect(page.Heading("Feeds")).ToBeVisibleAsync(Wait.Settle);
		await page.EvaluateAsync("document.fonts.ready");

		await Assert.That(requested.Where(url => url.Contains("/fonts/roboto/", StringComparison.Ordinal))).IsNotEmpty();
		await Assert
			.That(
				requested.Where(url => url.Contains("fonts.googleapis.com", StringComparison.Ordinal) || url.Contains("fonts.gstatic.com", StringComparison.Ordinal))
			)
			.IsEmpty();
	}

	[Test]
	public async Task ThemeToggle_WhenFlipped_PersistsAcrossNavigation()
	{
		var page = await E2EApp.AdminAt(Routes.Feeds);
		await Expect(page.Locator("html")).Not.ToHaveClassAsync(new Regex("dark"));

		await Expect(page.Locator("html"))
			.ToHaveAttributeAsync("data-base-color", new Regex("."), new LocatorAssertionsToHaveAttributeOptions { Timeout = Wait.Settle });
		await page.GetByLabel("Switch to dark mode").ClickAsync();

		await ShouldBeDarkAsync(page);
		await page.WaitForFunctionAsync("JSON.parse(localStorage.getItem('bb-theme') ?? '{}').isDarkMode === true");

		await page.VisitAsync(Routes.Browse);

		await ShouldBeDarkAsync(page);
	}

	private static async Task ShouldBeDarkAsync(IPage page)
	{
		await Expect(page.Locator("html")).ToHaveClassAsync(new Regex("dark"), new LocatorAssertionsToHaveClassOptions { Timeout = Wait.Settle });
		await Expect(page.GetByLabel("Switch to light mode")).ToBeVisibleAsync(Wait.Settle);
	}

	private static Task PressAsync(ILocator locator) => locator.DispatchEventAsync("click");
}
