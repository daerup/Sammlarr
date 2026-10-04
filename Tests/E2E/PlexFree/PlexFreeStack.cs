using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using E2E.Tests.Recording;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Options;

namespace E2E.Tests.PlexFree;

internal sealed class PlexFreeStack
{
	private const string Web = "blazor-ui";

	private const string WebLogCategory = "AppHost.Resources.blazor-ui";

	private const string KeystoreDirectory = "/home/wiremock/tls";

	private const string IndexerUrl = "http://wiremock:8080/newznab";

	private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(1);

	private static readonly TimeSpan ResetTimeout = TimeSpan.FromMinutes(3);

	private readonly DistributedApplication _app;

	private readonly SetupCodeSink _setupCodes;

	private readonly string _workDir;

	private readonly HttpClient _wireMock;

	private bool _leased;

	private PlexFreeStack(DistributedApplication app, SetupCodeSink setupCodes, string workDir)
	{
		_app = app;
		_setupCodes = setupCodes;
		_workDir = workDir;
		_wireMock = app.CreateHttpClient("wiremock", "http");
		BaseUrl = app.GetEndpoint(Web, "http").ToString();
		Radarr = ArrApi.For(app.GetEndpoint("radarr", "http"));
		Sonarr = ArrApi.For(app.GetEndpoint("sonarr", "http"));
	}

	internal string BaseUrl { get; }

	internal ArrApi Radarr { get; }

	internal ArrApi Sonarr { get; }

	internal Task<string> SetupCodeAsync() => _setupCodes.NextCodeAsync().WaitAsync(ReadyTimeout);

	internal static async Task<PlexFreeStack> LaunchAsync(TestCertificates certificates, string keyRing, string workDir)
	{
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>(
			["--LaunchSammlarr=false", "--RadarrSonarr=true", $"--ArrDataRoot={Path.Combine(workDir, "arr")}"],
			(options, _) => options.DisableDashboard = true
		);
		var setupCodes = new SetupCodeSink(WebLogCategory);
		builder.Services.AddLogging(logging => logging.AddProvider(setupCodes));
		builder.WaitThroughBusyDocker();

		var wiremock = builder
			.AddContainer("wiremock", "wiremock/wiremock", "latest")
			.WithHttpEndpoint(targetPort: 8080)
			.WithHttpHealthCheck("/__admin/health")
			.WithBindMount(WireMockStubs.MappingsDirectory, "/home/wiremock/mappings", isReadOnly: true)
			.WithBindMount(WireMockStubs.ResponsesDirectory, "/home/wiremock/__files", isReadOnly: true)
			.WithContainerFiles(KeystoreDirectory, [new ContainerFile { Name = "metadata.p12", SourcePath = certificates.KeystorePath }])
			.WithArgs(
				"--https-port",
				"443",
				"--https-keystore",
				$"{KeystoreDirectory}/metadata.p12",
				"--keystore-type",
				"PKCS12",
				"--keystore-password",
				certificates.Password,
				"--key-manager-password",
				certificates.Password
			);
		foreach (var host in MetadataHosts.All)
		{
			wiremock.WithContainerNetworkAlias(host);
		}

		foreach (var arr in builder.Resources.OfType<ContainerResource>().Where(resource => resource.Name is "radarr" or "sonarr").ToList())
		{
			builder
				.CreateResourceBuilder(arr)
				.WithEndpoint("http", endpoint => endpoint.Port = null)
				.WithContainerFiles("/etc/ssl/certs", [new ContainerOpenSSLCertificateFile { Name = "sammlarr-test-ca.pem", Contents = certificates.AuthorityPem }])
				.WaitFor(wiremock);
		}

		var wiremockHttp = wiremock.GetEndpoint("http");
		var web = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == Web);
		var webBuilder = builder
			.CreateResourceBuilder(web)
			.WithEndpoint("http", endpoint => endpoint.Port = null)
			.WithEnvironment("Sammlarr__Observability__LogLevel", nameof(LogVerbosity.Quiet))
			.WithEnvironment("Sammlarr__ConnectionString", $"Data Source={DatabasePath(workDir)}")
			.WithEnvironment("Sammlarr__DataProtection__KeyRingPath", keyRing)
			.WaitFor(wiremock);
		foreach (var provider in ProviderRecording.All.Where(provider => provider.Section != PlexOptions.SectionName))
		{
			foreach (var endpoint in provider.Endpoints)
			{
				webBuilder.WithEnvironment($"{provider.Section}__{endpoint.ConfigKey}", ReferenceExpression.Create($"{wiremockHttp}/"));
			}
		}

		foreach (var key in PlexUrlKeys())
		{
			webBuilder.WithEnvironment($"{PlexOptions.SectionName}__{key}", ReferenceExpression.Create($"{wiremockHttp}/plex-blocked/{key}"));
		}

		var app = await builder.BuildAsync();
		using var launch = new CancellationTokenSource(StackBoot.Timeout);
		await app.StartAsync(launch.Token);
		foreach (var resource in new[] { Web, "radarr", "sonarr" })
		{
			await app.ResourceNotifications.WaitForResourceHealthyAsync(resource, launch.Token);
		}

		var stack = new PlexFreeStack(app, setupCodes, workDir);
		await stack.AddIndexersAsync();
		return stack;
	}

	internal async Task<IReadOnlyList<string>> ReadyAsync()
	{
		var unmatched = _leased ? await ResetAsync() : await DrainAsync();
		_leased = true;
		return unmatched;
	}

	internal async Task<IReadOnlyList<string>> UnmatchedAsync()
	{
		var journal = JsonNode.Parse(await _wireMock.GetStringAsync("/__admin/requests/unmatched"))!;
		return [.. journal["requests"]!.AsArray().Select(entry => entry!["absoluteUrl"]!.GetValue<string>()).Distinct()];
	}

	internal async Task StopAsync()
	{
		_wireMock.Dispose();
		await _app.DisposeAsync();
	}

	private async Task<IReadOnlyList<string>> ResetAsync()
	{
		using var reset = new CancellationTokenSource(ResetTimeout);
		_setupCodes.Forget();
		await CommandAsync(KnownResourceCommands.StopCommand, reset.Token);
		await _app.ResourceNotifications.WaitForResourceAsync(Web, KnownResourceStates.TerminalStates, reset.Token);
		foreach (var suffix in new[] { "", "-wal", "-shm" })
		{
			File.Delete($"{DatabasePath(_workDir)}{suffix}");
		}

		await Radarr.SettleAsync();
		await Sonarr.SettleAsync();
		await Radarr.ClearAsync("movie");
		await Sonarr.ClearAsync("series");
		await AddIndexersAsync();
		await CommandAsync(KnownResourceCommands.StartCommand, reset.Token);
		await _app.ResourceNotifications.WaitForResourceHealthyAsync(Web, reset.Token);
		return await DrainAsync();
	}

	private async Task<IReadOnlyList<string>> DrainAsync()
	{
		await Radarr.SettleAsync();
		await Sonarr.SettleAsync();
		var unmatched = await UnmatchedAsync();
		(await _wireMock.DeleteAsync("/__admin/requests")).EnsureSuccessStatusCode();
		return unmatched;
	}

	private async Task AddIndexersAsync()
	{
		await AddIndexerAsync(Radarr, [2000, 2040]);
		await AddIndexerAsync(Sonarr, [5000, 5040]);
	}

	private static async Task AddIndexerAsync(ArrApi arr, int[] categories)
	{
		await arr.PostAsync(
			"indexer?forceSave=true",
			new
			{
				name = "Sammlarr test indexer",
				implementation = "Newznab",
				configContract = "NewznabSettings",
				protocol = "usenet",
				enableRss = true,
				enableAutomaticSearch = true,
				enableInteractiveSearch = true,
				priority = 25,
				tags = Array.Empty<int>(),
				fields = new IndexerField[] { new("baseUrl", IndexerUrl), new("apiPath", "/api"), new("apiKey", "test"), new("categories", categories) },
			}
		);
	}

	private async Task CommandAsync(string command, CancellationToken cancellationToken)
	{
		var result = await _app.ResourceCommands.ExecuteCommandAsync(Web, command, cancellationToken);
		if (!result.Success)
		{
			throw new InvalidOperationException($"{command} {Web}: {result.Message}");
		}
	}

	private static string DatabasePath(string workDir) => Path.Combine(workDir, "sammlarr.db");

	private static IEnumerable<string> PlexUrlKeys()
	{
		return typeof(PlexOptions)
			.GetProperties()
			.Where(property => property.PropertyType == typeof(string) && property.Name.EndsWith("Url", StringComparison.Ordinal))
			.Select(property => property.Name);
	}

	private sealed record IndexerField(string Name, object Value);
}
