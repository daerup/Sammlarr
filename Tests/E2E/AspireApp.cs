using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Harness;
using Microsoft.Playwright;

namespace E2E.Tests;

public static class AspireApp
{
	private static DistributedApplication? _app;
	private static IPlaywright? _playwright;
	private static IBrowser? _browser;
	private static string _baseUrl = "";
	private static string _authCookie = "";
	private static string _workDir = "";

	[Before(TestSession)]
	public static async Task StartAsync()
	{
		_workDir = Directory.CreateTempSubdirectory("sammlarr-e2e-").FullName;
		var dbPath = Path.Combine(_workDir, "sammlarr.db");
		var keyRing = Path.Combine(_workDir, "keys");
		Directory.CreateDirectory(keyRing);

		var (userId, displayName, role) = SammlarrTestHost.SeedDatabaseFile(dbPath);
		_authCookie = AuthCookie.Forge(keyRing, userId, displayName, role);

		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
			["--LaunchSammlarr=false"],
			(options, _) => options.DisableDashboard = true
		);
		var web = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "blazor-ui");
		builder
			.CreateResourceBuilder(web)
			.WithEnvironment("Sammlarr__ConnectionString", $"Data Source={dbPath}")
			.WithEnvironment("Sammlarr__DataProtection__KeyRingPath", keyRing);

		_app = await builder.BuildAsync();
		await _app.StartAsync();
		await _app.ResourceNotifications.WaitForResourceHealthyAsync("blazor-ui");
		_baseUrl = _app.GetEndpoint("blazor-ui", "http").ToString();

		_playwright = await Playwright.CreateAsync();
		_browser = await _playwright.Chromium.LaunchAsync(LaunchOptions());
	}

	[After(TestSession)]
	public static async Task StopAsync()
	{
		if (_browser is not null)
		{
			await _browser.DisposeAsync();
		}

		_playwright?.Dispose();

		if (_app is not null)
		{
			await _app.DisposeAsync();
		}

		if (Directory.Exists(_workDir))
		{
			Directory.Delete(_workDir, recursive: true);
		}
	}

	internal static async Task<IPage> OpenPageAsync()
	{
		var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = _baseUrl });
		return await context.NewPageAsync();
	}

	internal static async Task<IPage> OpenAuthenticatedPageAsync()
	{
		var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = _baseUrl });
		await context.AddCookiesAsync([
			new Cookie
			{
				Name = AuthCookie.Name,
				Value = _authCookie,
				Url = _baseUrl,
			},
		]);
		return await context.NewPageAsync();
	}

	private static BrowserTypeLaunchOptions LaunchOptions()
	{
		var headed = Environment.GetEnvironmentVariable("E2E_HEADED") is "true";
		return new BrowserTypeLaunchOptions { Headless = !headed, SlowMo = headed ? 750 : 0 };
	}
}
