using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Domain.Providers;
using E2E.Tests.Recording;
using Harness;
using Microsoft.Playwright;
using static VerifyTUnit.Verifier;

namespace E2E.Tests;

public static class E2EApp
{
	internal static bool IsRecording { get; } = Environment.GetEnvironmentVariable("E2E_LIVE") is "true";

	private static readonly SemaphoreSlim StartGate = new(1, 1);
	private static readonly Lock CaptureGate = new();
	private static readonly HashSet<string> Captured = [];
	private static DistributedApplication? _app;
	private static IPlaywright? _playwright;
	private static IBrowser? _browser;
	private static HttpClient? _wiremock;
	private static string _baseUrl = "";
	private static string _authCookie = "";
	private static string _workDir = "";
	private static bool _started;

	internal static async Task<IPage> OpenPageAsync()
	{
		await EnsureStartedAsync();
		var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = _baseUrl });
		return await context.NewPageAsync();
	}

	internal static async Task<IPage> OpenAuthenticatedPageAsync()
	{
		await EnsureStartedAsync();
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

	internal static async Task CaptureAsync(IPage page)
	{
		await page.WaitForTimeoutAsync(5000);
		var snapshot = await SnapshotAsync();
		foreach (var response in RecordedStubs.Write(snapshot, WireMockStubs.MappingsDirectory))
		{
			lock (CaptureGate)
			{
				if (!Captured.Add($"{response.Provider}/{response.Slug}"))
				{
					continue;
				}
			}

			await Verify(response.Body, "json")
				.UseDirectory(Path.Combine(WireMockStubs.ResponsesDirectory, response.Provider))
				.UseFileName(response.Slug)
				.DontScrubGuids()
				.DontScrubDateTimes();
		}
	}

	[After(TestSession)]
	public static async Task StopAsync()
	{
		if (!_started)
		{
			return;
		}

		_wiremock?.Dispose();
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

	private static async Task EnsureStartedAsync()
	{
		if (_started)
		{
			return;
		}

		await StartGate.WaitAsync();
		try
		{
			if (_started)
			{
				return;
			}

			await StartAsync();
			_started = true;
		}
		finally
		{
			StartGate.Release();
		}
	}

	private static async Task StartAsync()
	{
		_workDir = Directory.CreateTempSubdirectory("sammlarr-e2e-").FullName;
		var dbPath = Path.Combine(_workDir, "sammlarr.db");
		var keyRing = Path.Combine(_workDir, "keys");
		Directory.CreateDirectory(keyRing);

		var (userId, displayName, role) = IsRecording
			? SammlarrTestHost.SeedRecordingDatabase(dbPath, keyRing, Credentials<ApiKeyProvider>("_API_KEY"), Credentials<AuthProvider>("_TOKEN"))
			: SammlarrTestHost.SeedDatabaseFile(dbPath, keyRing, WireMockStubs.ExpectedTrendingTmdbId(), WireMockStubs.ExpectedTrendingTitle());
		_authCookie = AuthCookie.Forge(keyRing, userId, displayName, role);

		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
			["--LaunchSammlarr=false"],
			(options, _) => options.DisableDashboard = true
		);
		var wiremock = builder.AddContainer("wiremock", "wiremock/wiremock", "latest").WithHttpEndpoint(targetPort: 8080);
		var wiremockHttp = wiremock.GetEndpoint("http");
		if (!IsRecording)
		{
			wiremock
				.WithBindMount(WireMockStubs.MappingsDirectory, "/home/wiremock/mappings", isReadOnly: true)
				.WithBindMount(WireMockStubs.ResponsesDirectory, "/home/wiremock/__files", isReadOnly: true);
		}

		var web = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "blazor-ui");
		var webBuilder = builder
			.CreateResourceBuilder(web)
			.WithEnvironment("Sammlarr__ConnectionString", $"Data Source={dbPath}")
			.WithEnvironment("Sammlarr__DataProtection__KeyRingPath", keyRing)
			.WaitFor(wiremock);
		foreach (var provider in ProviderRecording.All)
		{
			var baseUrl = IsRecording ? ReferenceExpression.Create($"{wiremockHttp}{provider.Prefix}") : ReferenceExpression.Create($"{wiremockHttp}");
			webBuilder.WithEnvironment($"{provider.Section}__BaseUrl", baseUrl);
		}

		_app = await builder.BuildAsync();
		await _app.StartAsync();
		await _app.ResourceNotifications.WaitForResourceHealthyAsync("blazor-ui");
		_baseUrl = _app.GetEndpoint("blazor-ui", "http").ToString();
		_wiremock = _app.CreateHttpClient("wiremock", "http");
		if (IsRecording)
		{
			await ConfigureProxiesAsync();
		}

		var headed = Environment.GetEnvironmentVariable("E2E_HEADED") is "true";
		_playwright = await Playwright.CreateAsync();
		_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = !headed, SlowMo = headed ? 750 : 0 });
	}

	private static async Task ConfigureProxiesAsync()
	{
		foreach (var provider in ProviderRecording.All)
		{
			var mapping = new
			{
				priority = 5,
				request = new { urlPathPattern = $"{provider.Prefix}/.*" },
				response = new { proxyBaseUrl = provider.RealBaseUrl, proxyUrlPrefixToRemove = provider.Prefix },
			};
			await PostWithRetryAsync("/__admin/mappings", mapping);
		}
	}

	private static async Task<string> SnapshotAsync()
	{
		var prefixes = string.Join('|', ProviderRecording.All.Select(provider => provider.Name));
		var spec = new
		{
			persist = false,
			repeatsAsScenarios = false,
			filters = new { urlPathPattern = $"/({prefixes})/.*" },
		};
		var response = await _wiremock!.PostAsJsonAsync("/__admin/recordings/snapshot", spec);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsStringAsync();
	}

	private static async Task PostWithRetryAsync(string path, object body)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				var response = await _wiremock!.PostAsJsonAsync(path, body);
				response.EnsureSuccessStatusCode();
				return;
			}
			catch (HttpRequestException) when (attempt < 20)
			{
				await Task.Delay(500);
			}
		}
	}

	private static Dictionary<TProvider, string> Credentials<TProvider>(string suffix)
		where TProvider : struct, Enum =>
		Enum.GetValues<TProvider>()
			.Select(provider => (provider, secret: RecordingConfiguration.Value($"{provider.ToString()!.ToUpperInvariant()}{suffix}")))
			.Where(entry => !string.IsNullOrEmpty(entry.secret))
			.ToDictionary(entry => entry.provider, entry => entry.secret!);
}
