using System.Text.Json;
using System.Text.RegularExpressions;
using Harness;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Auth;

public class SessionTests
{
	private const string DeepLink = "/feeds";

	private const int PinId = 424242;

	private const string LoginRoute = "**/auth/*/login*";

	[Test]
	public async Task InvalidCookie_WhenPresent_RedirectsToSignIn()
	{
		var page = await E2EApp.OpenPageWithAuthCookieAsync("not-a-valid-protected-ticket");

		await page.GotoAsync(DeepLink);
		await page.WaitForURLAsync(new Regex("/signin"));

		await Assert.That(page.Url).Contains("/signin");
	}

	[Test]
	public async Task ProtectedDeepLink_AfterSignIn_LandsSomewhereDefined()
	{
		await using var pins = await E2EApp.StubAsync("/api/v2/pins", 200, PinBody(), "POST");
		await using var pin = await E2EApp.StubAsync($"/api/v2/pins/{PinId}", 200, PinBody());
		await using var account = await E2EApp.StubAsync("/api/v2/user", 200, AccountBody());

		var page = await E2EApp.OpenPageAsync();
		await page.GotoAsync(DeepLink);
		await page.WaitForURLAsync(new Regex("/signin"));
		await page.WaitForCircuitAsync();

		await Assert.That(page.Url).Contains($"returnUrl={Uri.EscapeDataString(DeepLink)}");

		await page.RouteAsync(LoginRoute, route => route.AbortAsync());
		var login = await page.RunAndWaitForRequestAsync(() => page.ButtonContaining("Sign in with Plex").ClickAsync(), LoginRoute);

		await Assert.That(login.Url).Contains($"returnUrl={Uri.EscapeDataString(DeepLink)}");

		await page.UnrouteAsync(LoginRoute);
		await page.VisitAsync($"/auth/plex/callback?pinId={PinId}&clientId={Guid.NewGuid()}&returnUrl={Uri.EscapeDataString(DeepLink)}");

		await Assert.That(new Uri(page.Url).AbsolutePath).IsEqualTo(DeepLink);
		await Expect(page.Heading("Feeds")).ToBeVisibleAsync();
	}

	private static string PinBody() =>
		JsonSerializer.Serialize(
			new
			{
				id = PinId,
				code = "e2e-code",
				product = "Sammlarr",
				trusted = true,
				qr = "e2e-qr",
				clientIdentifier = "e2e-client",
				expiresAt = "2030-01-01T00:00:00Z",
				authToken = "e2e-session-token",
				newRegistration = (string?)null,
			}
		);

	private static string AccountBody() =>
		JsonSerializer.Serialize(
			new
			{
				uuid = SammlarrTestHost.SeededMember.Id.ToString(),
				username = SammlarrTestHost.SeededMember.DisplayName,
				title = SammlarrTestHost.SeededMember.DisplayName,
				thumb = string.Empty,
				email = "e2e-redacted",
				friendlyName = SammlarrTestHost.SeededMember.DisplayName,
			}
		);
}
