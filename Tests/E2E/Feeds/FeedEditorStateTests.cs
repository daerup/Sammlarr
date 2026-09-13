using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

[NotInParallel]
public class FeedEditorStateTests
{
	private const string OptionalSection = "Filters, Sorting & Limits";

	[Test]
	public async Task Save_WhenNothingChangedOnEdit_IsDisabledUntilAFieldIsEdited()
	{
		var name = Name("Unchanged");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.TestAsync(page);
		await FeedEditor.SaveAsync(page);

		var row = page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name });
		await Expect(row).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
		await row.GetByLabel("Edit").ClickAsync();

		await Expect(FeedEditor.SaveButton(page)).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 60000 });

		await FeedEditor.FillAsync(page, "#name", $"{name} edited");

		await Expect(FeedEditor.SaveButton(page)).ToBeEnabledAsync();
	}

	[Test]
	public async Task Shortcut_WhenCtrlEnterPressed_RunsTheTest()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, Name("Shortcut"));
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(0);

		await page.Keyboard.PressAsync("Control+Enter");

		await FeedEditor.PreviewLoadedAsync(page);
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync((await FeedEditor.ServedTitlesAsync()).Count);
	}

	[Test]
	public async Task Apply_WhenAPreviewAffectingFieldChanges_ReplacesSave()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, Name("Pending"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);

		await FeedEditor.Option(page, "Strategy", "Replace").ClickAsync();

		await Expect(FeedEditor.SaveButton(page)).ToBeVisibleAsync();
		await Expect(FeedEditor.ApplyButton(page)).ToBeHiddenAsync();

		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable custom sorting").ClickAsync();

		await Expect(FeedEditor.ApplyButton(page)).ToBeVisibleAsync();
		await Expect(FeedEditor.SaveButton(page)).ToBeHiddenAsync();

		await FeedEditor.ApplyAsync(page);

		await Expect(FeedEditor.SaveButton(page)).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60000 });
		await Expect(FeedEditor.ApplyButton(page)).ToBeHiddenAsync();
	}

	[Test]
	public async Task Preview_WhenTheIdentifierChanges_ResetsToTest()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, Name("Reset"));
		await FeedEditor.TestAsync(page);
		await FeedEditor.PreviewLoadedAsync(page);
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync((await FeedEditor.ServedTitlesAsync()).Count);

		await FeedEditor.FillAsync(page, "#list-identifier", $"{FeedEditor.ListIdentifier}-changed");

		await Expect(FeedEditor.TestButton(page)).ToBeVisibleAsync();
		await Expect(FeedEditor.SaveButton(page)).ToBeHiddenAsync();
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(0);
		await Expect(page.GetByText("Run a test to preview the media items from your list.")).ToBeVisibleAsync();
	}

	private static string Name(string scenario) => $"E2E {scenario} {Guid.NewGuid().ToString("N")[..6]}";
}
