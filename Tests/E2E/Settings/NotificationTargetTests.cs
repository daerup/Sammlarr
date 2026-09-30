using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class NotificationTargetTests
{
	private const string NameInput = "input[placeholder='My webhook']";

	private const string UrlInput = "input[placeholder='https://example.com/hook']";

	private const string Unreachable = "https://e2e-unreachable.invalid/hook";

	[Test]
	public async Task Webhook_WhenCreated_AppearsAndCanBeEditedAndDeleted()
	{
		var name = TestData.Name("Hook");
		var renamed = TestData.Name("Rehook");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "notifications");

		await CreateWebhookAsync(page, name);
		await Expect(Client(page, name)).ToBeVisibleAsync();

		await Client(page, name).GetByRole(AriaRole.Button).First.ClickAsync();
		await page.Locator(NameInput).EnterAsync(renamed);
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
		await page.Button("Add client").ClickAsync();
		await page.Locator(NameInput).EnterAsync(TestData.Name("Pushover"));
		await SelectChannelAsync(page, "Pushover");

		await SaveAsync(page);

		await Expect(page.GetByText("App token and user key are required.")).ToBeVisibleAsync();

		await page.Button("Cancel").ClickAsync();

		await Expect(page.GetByText("App token and user key are required.")).ToBeHiddenAsync();
		await Expect(page.Locator(NameInput)).ToBeHiddenAsync();
	}

	[Test]
	public async Task TestPing_WhenSent_ReportsTheOutcomeInTheDialog()
	{
		var name = TestData.Name("Ping");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "notifications");
		await CreateWebhookAsync(page, name);

		await Client(page, name).GetByRole(AriaRole.Button).First.ClickAsync();
		await page.Button("Test connection").ClickAsync();

		await Expect(page.Locator("p.text-destructive")).ToBeVisibleAsync(Wait.Preview);

		await page.Button("Cancel").ClickAsync();
		await DeleteClientAsync(page, name);
	}

	[Test]
	public async Task EventTypes_WhenSelected_Persist()
	{
		var name = TestData.Name("Alerts");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await SettingsPage.VisitSectionAsync(page, "notifications");
		await CreateWebhookAsync(page, name);

		await page.Button("Add alert").ClickAsync();
		await SelectClientAsync(page, name);

		var events = page.Locator("label").Filter(new LocatorFilterOptions { Has = page.GetByRole(AriaRole.Checkbox) });
		var chosen = (await events.First.InnerTextAsync()).Trim();
		await events.First.GetByRole(AriaRole.Checkbox).ClickAsync();
		await SaveAsync(page);

		var alert = page.Locator("div.rounded-lg.border").Filter(new LocatorFilterOptions { HasText = chosen }).First;
		await Expect(alert).ToBeVisibleAsync(Wait.Settle);

		await SettingsPage.VisitSectionAsync(page, "notifications");

		await Expect(page.Locator("div.rounded-lg.border").Filter(new LocatorFilterOptions { HasText = chosen }).First).ToBeVisibleAsync();

		await DeleteClientAsync(page, name);
	}

	private static ILocator Client(IPage page, string name) =>
		page.Locator("div.flex-col.gap-4")
			.Filter(new LocatorFilterOptions { Has = page.Heading("Clients") })
			.Locator("div.items-center.rounded-lg.border")
			.Filter(new LocatorFilterOptions { HasText = name });

	private static async Task CreateWebhookAsync(IPage page, string name)
	{
		await page.Button("Add client").ClickAsync();
		await page.Locator(NameInput).EnterAsync(name);
		await page.Locator(UrlInput).EnterAsync(Unreachable);
		await SaveAsync(page);
		await page.Button("Done").ClickAsync();
	}

	private static Task SaveAsync(IPage page) => page.Button("Save").ClickAsync();

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
