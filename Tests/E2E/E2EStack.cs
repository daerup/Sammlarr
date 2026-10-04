using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Domain.Providers;
using E2E.Tests.Recording;
using Harness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace E2E.Tests;

internal sealed class E2EStack
{
	private const string CircuitFailure = "Unhandled exception in circuit";

	private const string CircuitLogCategory = "AppHost.Resources.blazor-ui";

	private readonly DistributedApplication _app;

	private readonly E2ESeed _seed;

	private E2EStack(DistributedApplication app, E2ESeed seed)
	{
		_app = app;
		_seed = seed;
		BaseUrl = app.GetEndpoint("blazor-ui", "http").ToString();
		WireMock = app.CreateHttpClient("wiremock", "http");
	}

	internal string BaseUrl { get; }

	internal HttpClient WireMock { get; }

	internal Uri WireMockUrl => WireMock.BaseAddress!;

	internal string AdminCookie => _seed.AdminCookie;

	internal string MemberCookie => _seed.MemberCookie;

	internal static E2ESeed Seed(bool live, string keyRing)
	{
		var workDir = Directory.CreateTempSubdirectory("sammlarr-e2e-").FullName;
		var dbPath = Path.Combine(workDir, "sammlarr.db");

		var admin = live
			? SammlarrTestHost.SeedRecordingDatabase(dbPath, keyRing, Credentials<ApiKeyProvider>("_API_KEY"), Credentials<AuthProvider>("_TOKEN"))
			: SammlarrTestHost.SeedDatabaseFile(dbPath, keyRing);

		return new E2ESeed(workDir, dbPath, keyRing, Forge(keyRing, admin), Forge(keyRing, SammlarrTestHost.SeededMember));
	}

	internal static async Task<E2EStack> LaunchAsync(E2ESeed seed, int index, bool live, Action<string> onCircuitFailure)
	{
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
			["--LaunchSammlarr=false", $"--RadarrSonarr={live.ToString().ToLowerInvariant()}", $"--ArrDataRoot={Path.Combine(seed.WorkDir, "arr")}"],
			(options, _) => options.DisableDashboard = true
		);
		builder.Services.AddLogging(logging => logging.AddProvider(new CircuitFailureSink(CircuitLogCategory, CircuitFailure, index, onCircuitFailure)));
		builder.WaitThroughBusyDocker();
		builder.UnpinArrPorts();
		var wiremock = builder.AddContainer("wiremock", "wiremock/wiremock", "latest").WithHttpEndpoint(targetPort: 8080);
		var wiremockHttp = wiremock.GetEndpoint("http");
		if (!live)
		{
			wiremock
				.WithBindMount(WireMockStubs.MappingsDirectory, "/home/wiremock/mappings", isReadOnly: true)
				.WithBindMount(WireMockStubs.ResponsesDirectory, "/home/wiremock/__files", isReadOnly: true);
		}

		var web = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "blazor-ui");
		var webBuilder = builder
			.CreateResourceBuilder(web)
			.WithEndpoint("http", endpoint => endpoint.Port = null)
			.WithEnvironment("Sammlarr__Observability__LogLevel", nameof(LogVerbosity.Quiet))
			.WithEnvironment("Sammlarr__ConnectionString", $"Data Source={seed.DbPath}")
			.WithEnvironment("Sammlarr__DataProtection__KeyRingPath", seed.KeyRing)
			.WaitFor(wiremock);
		foreach (var provider in ProviderRecording.All)
		{
			foreach (var endpoint in provider.Endpoints)
			{
				var baseUrl = live ? ReferenceExpression.Create($"{wiremockHttp}{endpoint.Prefix}/") : ReferenceExpression.Create($"{wiremockHttp}/");
				webBuilder.WithEnvironment($"{provider.Section}__{endpoint.ConfigKey}", baseUrl);
			}
		}

		var app = await builder.BuildAsync();
		using var boot = new CancellationTokenSource(StackBoot.Timeout);
		await app.StartAsync(boot.Token);
		await app.ResourceNotifications.WaitForResourceHealthyAsync("blazor-ui", boot.Token);
		foreach (var arr in live ? ProviderRecording.Apps : [])
		{
			await app.ResourceNotifications.WaitForResourceHealthyAsync(arr.Name, boot.Token);
		}

		var stack = new E2EStack(app, seed);
		if (live)
		{
			await stack.ConfigureProxiesAsync();
		}

		return stack;
	}

	internal Task<string> RequestJournalAsync() => WireMock.GetStringAsync("/__admin/requests");

	internal Task<string> UnmatchedAsync() => WireMock.GetStringAsync("/__admin/requests/unmatched");

	internal async Task<string> SnapshotAsync()
	{
		var prefixes = string.Join('|', ProviderRecording.AllEndpoints.Select(endpoint => endpoint.Name));
		var spec = new
		{
			persist = false,
			repeatsAsScenarios = false,
			filters = new { urlPathPattern = $"/({prefixes})/.*" },
		};
		var response = await WireMock.PostAsJsonAsync("/__admin/recordings/snapshot", spec);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsStringAsync();
	}

	internal async Task StopAsync()
	{
		WireMock.Dispose();
		await _app.DisposeAsync();
		if (Directory.Exists(_seed.WorkDir))
		{
			Directory.Delete(_seed.WorkDir, recursive: true);
		}
	}

	private async Task ConfigureProxiesAsync()
	{
		foreach (var endpoint in ProviderRecording.AllEndpoints)
		{
			var mapping = new
			{
				priority = 5,
				request = new { urlPathPattern = $"{endpoint.Prefix}/.*" },
				response = new { proxyBaseUrl = ProxyTarget(endpoint), proxyUrlPrefixToRemove = endpoint.Prefix },
			};
			await PostWithRetryAsync("/__admin/mappings", mapping);
		}
	}

	private string ProxyTarget(ProviderEndpoint endpoint)
	{
		return endpoint.IsApp
			? new UriBuilder(_app.GetEndpoint(endpoint.Name, "http")) { Host = "host.docker.internal" }
				.Uri.ToString()
				.TrimEnd('/')
			: endpoint.RealBaseUrl;
	}

	private async Task PostWithRetryAsync(string path, object body)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				var response = await WireMock.PostAsJsonAsync(path, body);
				response.EnsureSuccessStatusCode();
				return;
			}
			catch (HttpRequestException) when (attempt < 20)
			{
				await Task.Delay(500);
			}
		}
	}

	private static string Forge(string keyRingPath, SeededUser user) => AuthCookie.Forge(keyRingPath, user.Id, user.DisplayName, user.Role);

	private static Dictionary<TProvider, string> Credentials<TProvider>(string suffix)
		where TProvider : struct, Enum =>
		Enum.GetValues<TProvider>()
			.Select(provider => (provider, secret: RecordingConfiguration.Value($"{provider.ToString().ToUpperInvariant()}{suffix}")))
			.Where(entry => !string.IsNullOrEmpty(entry.secret))
			.ToDictionary(entry => entry.provider, entry => entry.secret!);
}

internal sealed record E2ESeed(string WorkDir, string DbPath, string KeyRing, string AdminCookie, string MemberCookie);
