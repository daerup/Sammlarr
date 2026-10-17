using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Common;

public class HoverButtonTests
{
	[Test]
	public async Task SignOutButton_InTheMenuNamingTheSignedInUser_RestsInTheDestructiveColourAndInvertsToItsFillOnHover()
	{
		var page = await E2EApp.AdminAt(Routes.Home);
		var profile = page.ButtonContaining("signed in as Test User");
		await Expect(profile).ToBeVisibleAsync();

		await profile.ClickAsync();
		var signOut = page.ButtonContaining("Sign out");
		await signOut.WaitForAsync();

		var restingColor = await signOut.EvaluateAsync<string>("el => getComputedStyle(el).color");
		var restingBackground = await signOut.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
		var destructive = await signOut.EvaluateAsync<string>(
			"el => { const probe = document.createElement('span'); probe.style.color = 'var(--destructive)'; el.parentElement.append(probe); const colour = getComputedStyle(probe).color; probe.remove(); return colour; }"
		);

		await signOut.HoverAsync();

		await Assert.That(restingBackground).IsEqualTo("rgba(0, 0, 0, 0)");
		await Assert.That(restingColor).IsEqualTo(destructive);
		await Expect(signOut).ToHaveCSSAsync("background-color", restingColor);
		await Expect(signOut).Not.ToHaveCSSAsync("color", restingColor);
	}
}
