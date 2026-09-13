using System.Globalization;
using Microsoft.Playwright;
using TUnit.Assertions.Enums;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedConfigurationTests
{
	private const string OptionalSection = "Filters, Sorting & Limits";

	private const string UnknownIdentifier = "e2e-unknown-list";

	[Test]
	public async Task Limit_WhenSet_CapsPreviewRows()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Limit"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var listed = (await FeedEditor.ServedTitlesAsync()).Count;
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(listed));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).ToHaveTextAsync(TestData.Count(0));

		var limit = listed - 2;
		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable limit").ClickAsync();
		await FeedEditor.FillAsync(page, "#limit", TestData.Count(limit));
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Total)).ToHaveTextAsync(TestData.Count(listed));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(limit));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Skipped)).ToHaveTextAsync(TestData.Count(listed - limit));

		await FeedEditor.Badge(page, PreviewBadge.Included).ClickAsync();

		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(limit);
	}

	[Test]
	public async Task Sorting_WhenCustom_ReordersPreviewRows()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Sort"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var sourceOrder = await FeedEditor.RowTitlesAsync(page);

		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable custom sorting").ClickAsync();
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
		var descending = await FeedEditor.RowTitlesAsync(page);

		await page.Button("Descending").ClickAsync();
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
		var ascending = await FeedEditor.RowTitlesAsync(page);

		await Assert.That(ascending).IsEquivalentTo(sourceOrder);
		await Assert.That(ascending).IsNotEquivalentTo(sourceOrder, CollectionOrdering.Matching);
		await Assert.That(descending).IsEquivalentTo(ascending.Reverse(), CollectionOrdering.Matching);
		await Assert.That(ascending).IsEquivalentTo(sourceOrder.Order(StringComparer.InvariantCulture), CollectionOrdering.Matching);
	}

	[Test]
	public async Task Filters_WhenEnabled_ChangeTheRowCount()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Filter"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		var titles = await FeedEditor.RowTitlesAsync(page);
		var kept = titles[0];
		await Expect(FeedEditor.Badge(page, PreviewBadge.Filtered)).ToHaveTextAsync(TestData.Count(0));

		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable filters").ClickAsync();
		await FeedEditor.AddConditionAsync(page, "Title", kept);
		await FeedEditor.ApplyAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await Expect(FeedEditor.Badge(page, PreviewBadge.Total)).ToHaveTextAsync(TestData.Count(titles.Count));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Included)).ToHaveTextAsync(TestData.Count(1));
		await Expect(FeedEditor.Badge(page, PreviewBadge.Filtered)).ToHaveTextAsync(TestData.Count(titles.Count - 1));

		await FeedEditor.Badge(page, PreviewBadge.Included).ClickAsync();
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(1);

		await Assert.That(await FeedEditor.RowTitlesAsync(page)).IsEquivalentTo([kept], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Strategy_WhenSaved_ShowsInTheFeedList()
	{
		var name = TestData.Name("Strategy");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.Option(page, "Strategy", "Replace").ClickAsync();
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);

		var row = page.Row(name);
		await Expect(row).ToBeVisibleAsync(Wait.Settle);
		await Expect(row.GetByText("Replace", new LocatorGetByTextOptions { Exact = true })).ToBeVisibleAsync();

		await row.GetByLabel("Edit").ClickAsync();

		await Expect(FeedEditor.Option(page, "Strategy", "Replace")).ToHaveAttributeAsync("aria-pressed", "");
		await Expect(FeedEditor.Option(page, "Strategy", "Append")).Not.ToHaveAttributeAsync("aria-pressed", "");
	}

	[Test]
	public async Task Schedule_WhenScheduled_PersistsAndRejectsInvalidCron()
	{
		const string hour = "4";
		const string minute = "30";
		var name = TestData.Name("Schedule");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.OpenOptionalSectionAsync(page, "Sync Frequency");
		await FeedEditor.Option(page, "Trigger", "Scheduled").ClickAsync();
		await page.GetByRole(AriaRole.Combobox).Filter(new LocatorFilterOptions { HasText = "Daily" }).ClickAsync();
		await page.Option("Expression").ClickAsync();
		await FeedEditor.TestAsync(page);

		await FeedEditor.FillAsync(page, CronField("Hour"), "99");
		await Expect(page.Locator(CronField("Hour"))).ToHaveAttributeAsync("aria-invalid", "true");
		await Expect(FeedEditor.SaveButton(page)).ToBeDisabledAsync();

		await FeedEditor.FillAsync(page, CronField("Minute"), minute);
		await FeedEditor.FillAsync(page, CronField("Hour"), hour);
		await FeedEditor.SaveAsync(page);

		var row = page.Row(name);
		await Expect(row).ToBeVisibleAsync(Wait.Settle);
		await row.GetByLabel("Edit").ClickAsync();

		await Expect(FeedEditor.Option(page, "Trigger", "Scheduled")).ToHaveAttributeAsync("aria-pressed", "");
		await Expect(page.Locator(CronField("Minute"))).ToHaveValueAsync(minute);
		await Expect(page.Locator(CronField("Hour"))).ToHaveValueAsync(hour);
	}

	[Test]
	public async Task Clone_WhenOpened_PrefillsAndSavesSeparately()
	{
		var suffix = Guid.NewGuid().ToString("N")[..6];
		var original = $"E2E Origin {suffix}";
		var copy = $"E2E Copy {suffix}";
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, original);
		await FeedEditor.Option(page, "Strategy", "Replace").ClickAsync();
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);

		var originalRow = page.Row(original);
		await Expect(originalRow).ToBeVisibleAsync(Wait.Settle);
		await originalRow.GetByLabel("Duplicate").ClickAsync();

		await Expect(page.GetByText("Clone feed").First).ToBeVisibleAsync();
		await Expect(page.Locator("#name")).ToHaveValueAsync(original);
		await Expect(page.Locator("#list-identifier")).ToHaveValueAsync(FeedEditor.ListIdentifier);
		await Expect(FeedEditor.Option(page, "Strategy", "Replace")).ToHaveAttributeAsync("aria-pressed", "");

		await FeedEditor.FillAsync(page, "#name", copy);
		await FeedEditor.SaveAsync(page);

		var copyRow = page.Row(copy);
		await Expect(copyRow).ToBeVisibleAsync(Wait.Settle);
		await Expect(originalRow).ToBeVisibleAsync();
		await Expect(copyRow.GetByText("Replace", new LocatorGetByTextOptions { Exact = true })).ToBeVisibleAsync();
	}

	[Test]
	public async Task EnabledToggle_WhenFlipped_RoundTrips()
	{
		var name = TestData.Name("Toggle");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.Toggle(page, "Enabled").ClickAsync();
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);

		var row = page.Row(name);
		await Expect(row).ToBeVisibleAsync(Wait.Settle);
		await Expect(row.GetByText("Disabled", new LocatorGetByTextOptions { Exact = true })).ToBeVisibleAsync();

		await row.GetByLabel("Edit").ClickAsync();
		await Expect(FeedEditor.Toggle(page, "Enabled")).ToHaveAttributeAsync("aria-checked", "false");
		await FeedEditor.Toggle(page, "Enabled").ClickAsync();
		await FeedEditor.SaveAsync(page);

		await Expect(row.GetByText("Enabled", new LocatorGetByTextOptions { Exact = true })).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task InvalidIdentifier_WhenTested_SurfacesNotFound()
	{
		await using var stub = await E2EApp.StubAsync($"/lists/{UnknownIdentifier}/items", 404, "{}");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.SelectMdblistAsync(page);
		await FeedEditor.FillAsync(page, "#list-identifier", UnknownIdentifier);
		await FeedEditor.TestAsync(page);

		await Expect(page.GetByText($"No list found with identifier '{UnknownIdentifier}'.")).ToBeVisibleAsync(Wait.Preview);
		await Expect(page.GetByText("Test failed")).ToBeVisibleAsync();
		await Expect(page.Button("Retry")).ToBeVisibleAsync();
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(0);
	}

	private static string CronField(string label) => $"input[aria-label^='{label}']";
}
