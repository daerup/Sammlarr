using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Feeds;

public class FeedEditorStateTests
{
	private const string OptionalSection = "Filters, Sorting & Limits";

	[Test]
	public async Task Save_WhenNothingChangedOnEdit_IsDisabledUntilAFieldIsEdited()
	{
		var name = TestData.Name("Unchanged");
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, name);
		await FeedEditor.LoadAsync(page);
		await FeedEditor.SaveAndCloseAsync(page);

		var card = page.FeedCard(name);
		await Expect(card).ToBeVisibleAsync(Wait.Settle);
		await card.Labelled("Edit").ClickAsync();

		await Expect(FeedEditor.SaveButton(page)).ToBeDisabledAsync(new LocatorAssertionsToBeDisabledOptions { Timeout = 60000 });

		await FeedEditor.FillAsync(page, "#name", $"{name} edited");

		await Expect(FeedEditor.SaveButton(page)).ToBeEnabledAsync();
		await Expect(FeedEditor.SaveStatus(page)).ToHaveTextAsync("Unsaved changes");
	}

	[Test]
	public async Task Save_ForANewFeed_StaysInTheEditorUnderTheEditAddress()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Stays"));
		await FeedEditor.PreviewLoadedAsync(page);

		await FeedEditor.SaveButton(page).ClickAsync();

		await Expect(FeedEditor.SavedStatus(page)).ToBeVisibleAsync(Wait.Settle);
		await Expect(page.Heading("Edit feed")).ToBeVisibleAsync();
		await Assert.That(new Uri(page.Url).AbsolutePath).StartsWith("/feeds/edit/");
		await Expect(FeedEditor.SaveButton(page)).ToBeDisabledAsync();
	}

	[Test]
	public async Task Close_WithUnsavedChanges_AsksBeforeLeaving()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Leave"));

		await FeedEditor.CloseButton(page).ClickAsync();
		await Expect(page.GetByText("Leave without saving?")).ToBeVisibleAsync();
		await page.Button("Keep editing").ClickAsync();
		await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo("/feeds/create");

		await FeedEditor.CloseButton(page).ClickAsync();
		await page.Button("Discard changes").ClickAsync();

		await Expect(page.Heading("Feeds")).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task Shortcut_WhenCtrlEnterPressed_Saves()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Shortcut"));
		await FeedEditor.PreviewLoadedAsync(page);

		await page.Keyboard.PressAsync("Control+Enter");

		await Expect(FeedEditor.SavedStatus(page)).ToBeVisibleAsync(Wait.Settle);
	}

	[Test]
	public async Task Identifier_WhenLeftAfterTyping_LoadsTheListWithoutATestStep()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");

		await FeedEditor.ConfigureAsync(page, TestData.Name("Leaves"));

		await FeedEditor.PreviewLoadedAsync(page);
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync((await FeedEditor.ServedTitlesAsync()).Count);
	}

	[Test]
	public async Task Preview_WhenAPreviewAffectingFieldChanges_CatchesUpOnItsOwn()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Pending"));
		await FeedEditor.PreviewLoadedAsync(page);

		await FeedEditor.Option(page, "Strategy", "Mirror").ClickAsync();
		await FeedEditor.CaughtUpAsync(page);

		await Expect(FeedEditor.SaveButton(page)).ToBeEnabledAsync();

		await FeedEditor.OpenOptionalSectionAsync(page, OptionalSection);
		await FeedEditor.Toggle(page, "Enable custom sorting").ClickAsync();
		await FeedEditor.CaughtUpAsync(page);

		await Expect(FeedEditor.SaveButton(page)).ToBeEnabledAsync();
	}

	[Test]
	public async Task Preview_WhenTheIdentifierIsTypedOver_DimsTheOldListUntilEnter()
	{
		var page = await E2EApp.OpenAuthenticatedPageAsync();
		await page.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(page, TestData.Name("Typed"));
		await FeedEditor.PreviewLoadedAsync(page);
		var listed = (await FeedEditor.ServedTitlesAsync()).Count;

		await page.Locator("#list-identifier").FillAsync($"{FeedEditor.ListIdentifier}-changed");

		await Expect(FeedEditor.PendingIdentifier(page)).ToBeVisibleAsync();
		await Expect(page.Locator(".preview-main-results")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("opacity-40"));
		await Expect(FeedEditor.Rows(page)).ToHaveCountAsync(listed);

		await page.Locator("#list-identifier").PressAsync("Enter");

		await Expect(FeedEditor.PendingIdentifier(page)).ToBeHiddenAsync(Wait.Settle);
	}
}
