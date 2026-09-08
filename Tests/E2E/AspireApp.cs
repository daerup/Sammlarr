using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;

namespace E2E.Tests;

public static class AspireApp
{
	private static DistributedApplication? _app;
	private static IPlaywright? _playwright;
	private static IBrowser? _browser;
	private static string _baseUrl = "";

	[Before(TestSession)]
	public static async Task StartAsync()
	{
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
			["--LaunchSammlarr=false"],
			(options, _) => options.DisableDashboard = Environment.GetEnvironmentVariable("E2E_DASHBOARD") is not "1" and not "true"
		);
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
	}

	internal static async Task<IPage> OpenPageAsync()
	{
		var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = _baseUrl });
		return await context.NewPageAsync();
	}

	private static BrowserTypeLaunchOptions LaunchOptions()
	{
		var headed = Environment.GetEnvironmentVariable("E2E_HEADED") is "true";
		return new BrowserTypeLaunchOptions { Headless = !headed, SlowMo = headed ? 750 : 0 };
	}
}
