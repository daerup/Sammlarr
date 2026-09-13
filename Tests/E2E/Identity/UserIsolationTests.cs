using E2E.Tests.Feeds;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Identity;

[NotInParallel]
public class UserIsolationTests
{
	[Test]
	public async Task TwoUsers_DoNotSeeEachOthersFeeds()
	{
		var name = $"E2E Private {Guid.NewGuid().ToString("N")[..6]}";
		var admin = await E2EApp.OpenAuthenticatedPageAsync();
		await admin.VisitAsync("/feeds/create");
		await FeedEditor.ConfigureAsync(admin, name);
		await FeedEditor.TestAsync(admin);
		await FeedEditor.SaveAsync(admin);
		await Expect(admin.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name }))
			.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });

		var member = await E2EApp.OpenMemberPageAsync();
		await member.VisitAsync("/feeds");

		await Expect(member.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Feeds" })).ToBeVisibleAsync();
		await Expect(member.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = name })).ToBeHiddenAsync();
	}
}
