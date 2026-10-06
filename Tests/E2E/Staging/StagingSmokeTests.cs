using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using E2E.Tests.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Web.Staging;
using static Microsoft.Playwright.Assertions;

namespace E2E.Tests.Staging;

[Staging]
[NotInParallel]
public class StagingSmokeTests
{
	[Test]
	public async Task Staging_OnAnEmptyDataDirectory_ComesUpSeededWithEveryServiceWired()
	{
		var root = Directory.CreateTempSubdirectory("sammlarr-staging-e2e-");
		try
		{
			var (app, admin, session) = await StartAsync(root.FullName);
			await using var _ = app;
			using var playwright = await Playwright.CreateAsync();
			await using var browser = await BrowserPages.LaunchAsync(playwright);
			await using var context = await BrowserPages.OpenAsync(browser, app.GetEndpoint("sammlarr", "http").ToString().TrimEnd('/'));
			var page = await context.NewPageAsync();

			await page.VisitAsync(Routes.Home);
			await page.Labelled("Username").EnterAsync(admin.Name);
			await page.Labelled("Password").EnterAsync(admin.Password);
			await page.Button("Sign in").ClickAsync();
			await page.ShouldBeAtAsync(Routes.Home);
			await page.VisitAsync(SettingsPage.Section("providers"));

			await Expect(SettingsPage.MapService(page, "Radarr + Sonarr"))
				.ToContainTextAsync("Connected", new LocatorAssertionsToContainTextOptions { Timeout = Wait.Settle });
			await CatchesAWebhookAndAPushoverMessageAsync(app.GetEndpoint("catcher", "http"), session);
			await ReadsPlexButKeepsWatchlistWritesInWireMockAsync(app.GetEndpoint("wiremock", "http"));
			await StartsThePlexSignInThroughWireMockAsync(app.GetEndpoint("sammlarr", "http"));
		}
		finally
		{
			root.Delete(recursive: true);
		}
	}

	private static async Task CatchesAWebhookAndAPushoverMessageAsync(Uri catcher, string session)
	{
		using var http = new HttpClient { BaseAddress = catcher };
		List<(string Url, string Body)> caught = [];
		await Poll.UntilAsync(
			async () =>
			{
				var requests = (await http.GetFromJsonAsync<JsonArray>($"api/session/{session}/requests"))!;
				caught = [.. requests.Select(request => (request!["url"]!.GetValue<string>(), Body(request["request_payload_base64"]!.GetValue<string>())))];
				return caught.Any(request => request.Url.EndsWith("/webhook", StringComparison.Ordinal))
					&& caught.Any(request =>
						request.Url.EndsWith("/messages.json", StringComparison.Ordinal) && !request.Body.Contains("message=%28empty%29", StringComparison.Ordinal)
					);
			},
			() => $"The catcher did not get a webhook and a Pushover message with text, only {string.Join(", ", caught.Select(request => request.Url))}"
		);
	}

	private static string Body(string base64) => Encoding.UTF8.GetString(Convert.FromBase64String(base64));

	private static async Task ReadsPlexButKeepsWatchlistWritesInWireMockAsync(Uri wiremock)
	{
		using var http = new HttpClient { BaseAddress = wiremock };

		using var read = await http.GetAsync("plex/plex.tv/api/v2/user");
		using var write = await http.PutAsync("plex/vod.provider.plex.tv/actions/addToWatchlist?ratingKey=staging", new StringContent(string.Empty));

		await Assert.That(read.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
		await Assert.That(write.Headers.GetValues("X-Staging")).Contains("Plex write caught by WireMock");
	}

	private static async Task StartsThePlexSignInThroughWireMockAsync(Uri sammlarr)
	{
		using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = sammlarr };

		using var login = await http.GetAsync("auth/plex/login");

		await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
		await Assert.That(login.Headers.Location!.Host).IsEqualTo("app.plex.tv");
	}

	private static async Task<(DistributedApplication App, StagingPersona Admin, string CatcherSession)> StartAsync(string root)
	{
		using var timeout = new CancellationTokenSource(StackBoot.Timeout);
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
			["--Staging=true", $"--StagingRoot={root}", "--Parameters:tmdb-api-key=e2e-tmdb-key", "--Parameters:mdblist-api-key="],
			(options, _) => options.DisableDashboard = true
		);
		builder.Services.AddLogging(logging => logging.AddFilter("AppHost.Resources", LogLevel.Warning));
		builder.WaitThroughBusyDocker();
		foreach (var container in builder.Resources.OfType<ContainerResource>().ToList())
		{
			foreach (var name in container.Annotations.OfType<ContainerNameAnnotation>().ToList())
			{
				container.Annotations.Remove(name);
			}

			builder.CreateResourceBuilder(container).WithLifetime(ContainerLifetime.Session).WithEndpoint("http", endpoint => endpoint.Port = null);
		}

		var admin = builder.Configuration.GetSection("Staging:Personas:Admin").Get<StagingPersona>()!;
		var session = builder.Configuration["Staging:CatcherSession"]!;
		var app = await builder.BuildAsync(timeout.Token);
		await app.StartAsync(timeout.Token);
		await app.ResourceNotifications.WaitForResourceHealthyAsync("sammlarr", timeout.Token);
		return (app, admin, session);
	}
}
