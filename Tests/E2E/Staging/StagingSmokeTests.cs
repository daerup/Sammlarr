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
	public async Task Staging_OnAnEmptyDataDirectory_SignsTheSeededAdminInWithRadarrAndSonarrConnected()
	{
		var root = Directory.CreateTempSubdirectory("sammlarr-staging-e2e-");
		try
		{
			var (app, admin) = await StartAsync(root.FullName);
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
		}
		finally
		{
			root.Delete(recursive: true);
		}
	}

	private static async Task<(DistributedApplication App, StagingPersona Admin)> StartAsync(string root)
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
		var app = await builder.BuildAsync(timeout.Token);
		await app.StartAsync(timeout.Token);
		await app.ResourceNotifications.WaitForResourceHealthyAsync("sammlarr", timeout.Token);
		return (app, admin);
	}
}
