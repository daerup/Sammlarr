using E2E.Tests.Feeds;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Identity;

public class UserIsolationTests
{
	[Test]
	public async Task TwoUsers_DoNotSeeEachOthersFeeds()
	{
		var name = TestData.Name("Private");
		var admin = await E2EApp.AdminAt(Routes.CreateFeed);
		await FeedEditor.ConfigureAsync(admin, name);
		await FeedEditor.TestAsync(admin);
		await FeedEditor.SaveAsync(admin);
		await Expect(admin.Row(name)).ToBeVisibleAsync(Wait.Settle);

		var member = await E2EApp.MemberAt(Routes.Feeds);

		await Expect(member.Heading("Feeds")).ToBeVisibleAsync();
		await Expect(member.Row(name)).ToBeHiddenAsync();
	}
}
