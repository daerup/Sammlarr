using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Settings;

public class AboutTests
{
	[Test]
	public async Task FooterVersion_WhenClicked_OpensAboutShowingTheSameVersion()
	{
		var page = await E2EApp.AdminAt(Routes.Home);
		var footerVersion = page.Locator("footer a[href='/settings?section=about']");
		var version = (await footerVersion.InnerTextAsync()).Trim();

		await footerVersion.ClickAsync();

		await Expect(page.Heading("About")).ToBeVisibleAsync();
		await Expect(page.Locator(".version-number")).ToHaveTextAsync(version);
	}

	[Test]
	public async Task FooterVersion_IsMutedUntilHovered()
	{
		var page = await E2EApp.AdminAt(Routes.Home);
		var footerVersion = page.Locator("footer a[href='/settings?section=about']");
		var resting = await footerVersion.EvaluateAsync<string>("el => getComputedStyle(el).color");
		var muted = await footerVersion.EvaluateAsync<string>(
			"el => { const probe = document.createElement('span'); probe.style.color = 'var(--muted-foreground)'; el.parentElement.append(probe); const colour = getComputedStyle(probe).color; probe.remove(); return colour; }"
		);

		await footerVersion.HoverAsync();
		await page.WaitForTimeoutAsync(300);
		var hovered = await footerVersion.EvaluateAsync<string>("el => getComputedStyle(el).color");

		await Assert.That(resting).IsEqualTo(muted);
		await Assert.That(hovered).IsNotEqualTo(resting);
	}

	[Test]
	public async Task About_WhenSignedInAsMember_IsOfferedInTheSectionNav()
	{
		var page = await E2EApp.MemberAt(Routes.Settings);

		await Expect(page.Button("About")).ToBeVisibleAsync();
	}
}
