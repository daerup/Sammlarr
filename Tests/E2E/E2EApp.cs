using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Domain.Providers;
using E2E.Tests.Recording;
using Harness;
using Microsoft.Playwright;

namespace E2E.Tests;

public static class E2EApp
{
	internal static bool IsLive { get; } = Environment.GetEnvironmentVariable("E2E_LIVE") is "true";

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

	internal static async Task<string> UnmatchedAsync()
	{
		await EnsureStartedAsync();
		return await _wiremock!.GetStringAsync("/__admin/requests/unmatched");
	}

	internal static async Task<string> RequestJournalAsync()
	{
		await EnsureStartedAsync();
		return await _wiremock!.GetStringAsync("/__admin/requests");
	}

	internal static async Task<string> ServedBodyAsync(string urlPath)
	{
		var served = (JsonNode.Parse(await RequestJournalAsync())?["requests"]?.AsArray() ?? []).First(entry =>
			entry?["request"]?["url"]?.GetValue<string>()?.Split('?')[0].EndsWith(urlPath, StringComparison.Ordinal) is true
		);
		return served!["response"]!["body"]!.GetValue<string>();
	}

	private static async Task<bool> BootstrapAsync(RecordedResponse response)
	{
		var fixturePath = FixturePath(response);
		if (File.Exists(fixturePath))
		{
			return false;
		}

		Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
		await WriteFixtureAsync(fixturePath, response.Body);
		return true;
	}

	private static Task WriteFixtureAsync(string path, string body) => File.WriteAllTextAsync(path, body.ReplaceLineEndings("\n"));

	private static string FixturePath(RecordedResponse response) =>
		Path.Combine(WireMockStubs.ResponsesDirectory, response.Provider, $"{response.Slug}.verified.json");

	[After(TestSession)]
	public static async Task StopAsync()
	{
		if (!_started)
		{
			return;
		}

		if (Environment.GetEnvironmentVariable("E2E_AUDIT") is "true")
		{
			await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "e2e-served.txt"), Urls(await RequestJournalAsync()));
			await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "e2e-unmatched.txt"), Urls(await UnmatchedAsync()));
		}

		if (IsLive)
		{
			await CaptureRemainingAsync();
			await PruneUncapturedFixturesAsync();
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

	private static IEnumerable<string> Urls(string journalJson) =>
		(JsonNode.Parse(journalJson)?["requests"]?.AsArray() ?? [])
			.Select(entry => (entry?["request"] ?? entry)?["url"]?.GetValue<string>())
			.OfType<string>()
			.Distinct();

	private static async Task CaptureRemainingAsync()
	{
		var recorded = RecordedStubs.Write(await SnapshotAsync(), WireMockStubs.MappingsDirectory).Where(FirstCapture).ToList();
		var committed = new List<RecordedResponse>();
		foreach (var response in recorded)
		{
			if (WireMockStubs.PolicyFor(response.Path) is FixturePolicy.Frozen || !await BootstrapAsync(response))
			{
				committed.Add(response);
			}
		}

		var drift = new List<string>();
		foreach (var response in committed.Where(response => File.Exists(FixturePath(response))))
		{
			drift.AddRange(await CompareAsync(response));
		}

		await Assert.That(drift).IsEmpty();
	}

	private static bool FirstCapture(RecordedResponse response)
	{
		lock (CaptureGate)
		{
			return Captured.Add($"{response.Provider}/{response.Slug}");
		}
	}

	private static async Task<IEnumerable<string>> CompareAsync(RecordedResponse response)
	{
		var fixturePath = FixturePath(response);
		var committed = await File.ReadAllTextAsync(fixturePath);
		var problems =
			WireMockStubs.PolicyFor(response.Path) is FixturePolicy.Value
				? ValueDrift(response.Body, committed)
				: JsonContract.Incompatibilities(response.Body, committed);

		var receivedPath = $"{fixturePath[..^".verified.json".Length]}.received.json";
		if (problems.Count > 0)
		{
			await WriteFixtureAsync(receivedPath, response.Body);
		}
		else
		{
			File.Delete(receivedPath);
		}

		return problems.Select(problem => $"{response.Provider}/{response.Slug}: {problem}");
	}

	private static IReadOnlyList<string> ValueDrift(string live, string committed) =>
		string.Equals(live.ReplaceLineEndings("\n"), committed.ReplaceLineEndings("\n"), StringComparison.Ordinal)
			? []
			: ["the live response no longer matches the committed value"];

	private static async Task PruneUncapturedFixturesAsync()
	{
		HashSet<string> captured;
		lock (CaptureGate)
		{
			captured = [.. Captured];
		}

		var capturedProviders = captured.Select(key => key.Split('/')[0]).ToHashSet();
		foreach (var mappingFile in Directory.EnumerateFiles(WireMockStubs.MappingsDirectory, "*.json", SearchOption.AllDirectories))
		{
			var provider = Path.GetFileName(Path.GetDirectoryName(mappingFile)!);
			if (provider == "mappings" || !capturedProviders.Contains(provider) || captured.Contains($"{provider}/{Path.GetFileNameWithoutExtension(mappingFile)}"))
			{
				continue;
			}

			var mapping = JsonNode.Parse(await File.ReadAllTextAsync(mappingFile));
			File.Delete(mappingFile);
			if (mapping?["response"]?["bodyFileName"]?.GetValue<string>() is { } bodyFileName)
			{
				File.Delete(Path.Combine(WireMockStubs.ResponsesDirectory, bodyFileName));
			}
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

		var (userId, displayName, role) = IsLive
			? SammlarrTestHost.SeedRecordingDatabase(dbPath, keyRing, Credentials<ApiKeyProvider>("_API_KEY"), Credentials<AuthProvider>("_TOKEN"))
			: SammlarrTestHost.SeedDatabaseFile(dbPath, keyRing, WireMockStubs.ExpectedTrendingTmdbId(), WireMockStubs.ExpectedTrendingTitle());
		_authCookie = AuthCookie.Forge(keyRing, userId, displayName, role);

		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
			["--LaunchSammlarr=false"],
			(options, _) => options.DisableDashboard = true
		);
		var wiremock = builder.AddContainer("wiremock", "wiremock/wiremock", "latest").WithHttpEndpoint(targetPort: 8080);
		var wiremockHttp = wiremock.GetEndpoint("http");
		if (!IsLive)
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
			foreach (var endpoint in provider.Endpoints)
			{
				var baseUrl = IsLive ? ReferenceExpression.Create($"{wiremockHttp}{endpoint.Prefix}/") : ReferenceExpression.Create($"{wiremockHttp}/");
				webBuilder.WithEnvironment($"{provider.Section}__{endpoint.ConfigKey}", baseUrl);
			}
		}

		_app = await builder.BuildAsync();
		await _app.StartAsync();
		await _app.ResourceNotifications.WaitForResourceHealthyAsync("blazor-ui");
		_baseUrl = _app.GetEndpoint("blazor-ui", "http").ToString();
		_wiremock = _app.CreateHttpClient("wiremock", "http");
		if (IsLive)
		{
			await ConfigureProxiesAsync();
		}

		var headed = Environment.GetEnvironmentVariable("E2E_HEADED") is "true";
		_playwright = await Playwright.CreateAsync();
		_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = !headed, SlowMo = headed ? 2000 : 0 });
	}

	private static async Task ConfigureProxiesAsync()
	{
		foreach (var endpoint in ProviderRecording.AllEndpoints)
		{
			var mapping = new
			{
				priority = 5,
				request = new { urlPathPattern = $"{endpoint.Prefix}/.*" },
				response = new { proxyBaseUrl = endpoint.RealBaseUrl, proxyUrlPrefixToRemove = endpoint.Prefix },
			};
			await PostWithRetryAsync("/__admin/mappings", mapping);
		}
	}

	private static async Task<string> SnapshotAsync()
	{
		var prefixes = string.Join('|', ProviderRecording.AllEndpoints.Select(endpoint => endpoint.Name));
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
