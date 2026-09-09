using Microsoft.Playwright;

namespace E2E.Tests;

public class HoverButtonTests
{
	[Test]
	public async Task SignOutButton_OnHover_InvertsToTheDestructiveFill()
	{
		var page = await AspireApp.OpenAuthenticatedPageAsync();
		await page.GotoAsync("/");

		await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "signed in as Test User" }).ClickAsync();
		var signOut = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign out" });
		await signOut.WaitForAsync();

		var restingColor = await signOut.EvaluateAsync<string>("el => getComputedStyle(el).color");
		var restingBackground = await signOut.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
		var tint = await signOut.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--tint').trim()");
		var destructive = await signOut.EvaluateAsync<string>("el => getComputedStyle(document.documentElement).getPropertyValue('--destructive').trim()");

		await signOut.HoverAsync();
		await page.WaitForTimeoutAsync(300);
		var hoverColor = await signOut.EvaluateAsync<string>("el => getComputedStyle(el).color");
		var hoverBackground = await signOut.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");

		await Assert.That(restingBackground).IsEqualTo("rgba(0, 0, 0, 0)");
		await Assert.That(tint).IsEqualTo(destructive);
		await Assert.That(hoverBackground).IsEqualTo(restingColor);
		await Assert.That(hoverColor).IsNotEqualTo(restingColor);
	}
}
