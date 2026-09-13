using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

[NotInParallel]
public class NotificationTargetTests
{
	private const string NameInput = "input[placeholder='My webhook']";

	private const string UrlInput = "input[placeholder='https://example.com/hook']";

	private const string Unreachable = "https://e2e-unreachable.invalid/hook";

	[Test]
	public async Task Webhook_WhenCreated_AppearsAndCanBeEditedAndDeleted()
	{
		var name = Name("Hook");
		var renamed = Name("Rehook");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "notifications");

		await CreateWebhookAsync(page, name);
		await Expect(Client(page, name)).ToBeVisibleAsync();

		await Client(page, name).GetByRole(AriaRole.Button).First.ClickAsync();
		await FillAsync(page, NameInput, renamed);
		await SaveAsync(page);

		await Expect(Client(page, renamed)).ToBeVisibleAsync();
		await Expect(Client(page, name)).ToBeHiddenAsync();

		await DeleteClientAsync(page, renamed);

		await Expect(page.GetByText("No clients yet. Add a webhook or Pushover connection.")).ToBeVisibleAsync();
	}

	[Test]
	public async Task Pushover_WhenTokenOrUserKeyMissing_ShowsValidationError()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "notifications");
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add client", Exact = true }).ClickAsync();
		await FillAsync(page, NameInput, Name("Pushover"));
		await SelectChannelAsync(page, "Pushover");

		await SaveAsync(page);

		await Expect(page.GetByText("App token and user key are required.")).ToBeVisibleAsync();

		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync();

		await Expect(page.GetByText("App token and user key are required.")).ToBeHiddenAsync();
		await Expect(page.Locator(NameInput)).ToBeHiddenAsync();
	}

	[Test]
	public async Task TestPing_WhenSent_ReportsTheOutcomeInTheDialog()
	{
		var name = Name("Ping");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "notifications");
		await CreateWebhookAsync(page, name);

		await Client(page, name).GetByRole(AriaRole.Button).First.ClickAsync();
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Test connection", Exact = true }).ClickAsync();

		await Expect(page.Locator("p.text-destructive")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60000 });

		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cancel", Exact = true }).ClickAsync();
		await DeleteClientAsync(page, name);
	}

	[Test]
	public async Task EventTypes_WhenSelected_Persist()
	{
		var name = Name("Alerts");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "notifications");
		await CreateWebhookAsync(page, name);

		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add alert", Exact = true }).ClickAsync();
		await SelectClientAsync(page, name);

		var events = page.Locator("label").Filter(new LocatorFilterOptions { Has = page.GetByRole(AriaRole.Checkbox) });
		var chosen = (await events.First.InnerTextAsync()).Trim();
		await events.First.GetByRole(AriaRole.Checkbox).ClickAsync();
		await SaveAsync(page);

		var alert = page.Locator("div.rounded-lg.border").Filter(new LocatorFilterOptions { HasText = chosen }).First;
		await Expect(alert).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });

		await SettingsPage.VisitSectionAsync(page, "notifications");

		await Expect(page.Locator("div.rounded-lg.border").Filter(new LocatorFilterOptions { HasText = chosen }).First).ToBeVisibleAsync();

		await DeleteClientAsync(page, name);
	}

	private static string Name(string scenario) => $"E2E {scenario} {Guid.NewGuid().ToString("N")[..6]}";

	private static ILocator Client(IPage page, string name) =>
		page.Locator("div.items-center.rounded-lg.border").Filter(new LocatorFilterOptions { HasText = name }).Last;

	private static async Task FillAsync(IPage page, string selector, string value)
	{
		var input = page.Locator(selector);
		await input.FillAsync(string.Empty);
		await input.PressSequentiallyAsync(value, new LocatorPressSequentiallyOptions { Delay = 50 });
		await page.WaitForTimeoutAsync(500);
	}

	private static async Task CreateWebhookAsync(IPage page, string name)
	{
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add client", Exact = true }).ClickAsync();
		await FillAsync(page, NameInput, name);
		await FillAsync(page, UrlInput, Unreachable);
		await SaveAsync(page);
		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Done", Exact = true }).ClickAsync();
	}

	private static Task SaveAsync(IPage page) => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();

	private static async Task SelectChannelAsync(IPage page, string channel)
	{
		await page.GetByRole(AriaRole.Combobox).First.ClickAsync();
		await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = channel, Exact = true }).ClickAsync();
	}

	private static async Task SelectClientAsync(IPage page, string name)
	{
		await page.GetByRole(AriaRole.Combobox).First.DispatchEventAsync("click");
		await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = name }).DispatchEventAsync("click");
	}

	private static async Task DeleteClientAsync(IPage page, string name)
	{
		await Client(page, name).GetByRole(AriaRole.Button).Last.ClickAsync();
		await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = $"Delete {name}?" })).ToBeVisibleAsync();
		await page.GetByRole(AriaRole.Alertdialog).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete", Exact = true }).ClickAsync();
	}
}
