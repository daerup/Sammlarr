using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using E2E.Tests.Recording;
using Microsoft.Playwright;

namespace E2E.Tests;

public static class E2EApp
{
	internal static bool IsLive { get; } = Environment.GetEnvironmentVariable("E2E_LIVE") is "true";

	internal static int StackCount { get; } = IsLive ? 1 : 8;

	private static readonly Lazy<Task> Boot = new(StartAsync);
	private static readonly Lock CaptureGate = new();
	private static readonly HashSet<string> Captured = [];
	private static readonly HashSet<string> Stubbed = [];
	private static readonly List<string> CircuitFailures = [];
	private static readonly ConcurrentDictionary<string, Lazy<Task<Lease>>> Leases = [];
	private static readonly Channel<E2EStack> Idle = Channel.CreateUnbounded<E2EStack>();
	private const string PlaceholderArtwork = """
		<svg xmlns="http://www.w3.org/2000/svg" width="200" height="300" viewBox="0 0 200 300">
		  <rect width="200" height="300" fill="#334155"/>
		  <g fill="none" stroke="#94a3b8" stroke-width="5" stroke-linejoin="round" stroke-linecap="round">
		    <rect x="66" y="98" width="68" height="52" rx="6"/>
		    <circle cx="84" cy="114" r="6"/>
		    <path d="M66 142 l22 -18 l16 13 l10 -8 l20 17"/>
		  </g>
		  <text x="100" y="186" text-anchor="middle" font-family="system-ui, sans-serif" font-size="26" font-weight="700" fill="#e2e8f0">E2E</text>
		  <text x="100" y="203" text-anchor="middle" font-family="system-ui, sans-serif" font-size="12" letter-spacing="1.5" fill="#94a3b8">PLACEHOLDER</text>
		</svg>
		""";

	private static E2EStack[] _stacks = [];
	private static IPlaywright? _playwright;
	private static IBrowser? _browser;
	private static string _keyRing = "";

	internal static Task<IPage> OpenPageAsync() => NewPageAsync(_ => null);

	internal static Task<IPage> OpenAuthenticatedPageAsync() => NewPageAsync(stack => stack.AdminCookie);

	internal static Task<IPage> OpenMemberPageAsync() => NewPageAsync(stack => stack.MemberCookie);

	internal static Task<IPage> OpenPageWithAuthCookieAsync(string value) => NewPageAsync(_ => value);

	internal static Task<IPage> AdminAt(string route) => OpenAtAsync(OpenAuthenticatedPageAsync, route);

	internal static Task<IPage> MemberAt(string route) => OpenAtAsync(OpenMemberPageAsync, route);

	private static async Task<IPage> OpenAtAsync(Func<Task<IPage>> open, string route)
	{
		var page = await open();
		await page.VisitAsync(route);
		return page;
	}

	private static async Task<IPage> NewPageAsync(Func<E2EStack, string?> cookie)
	{
		var lease = await LeaseAsync();
		var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = lease.Stack.BaseUrl });
		lease.Contexts.Add(context);
		await context.RouteAsync(url => !url.StartsWith(lease.Stack.BaseUrl, StringComparison.Ordinal), ServeArtworkLocallyAsync);
		await context.AddInitScriptAsync(Locators.MarkListeningInputs);
		if (cookie(lease.Stack) is { } value)
		{
			await context.AddCookiesAsync([
				new Cookie
				{
					Name = AuthCookie.Name,
					Value = value,
					Url = lease.Stack.BaseUrl,
				},
			]);
		}

		return await context.NewPageAsync();
	}

	private static Task ServeArtworkLocallyAsync(IRoute route) =>
		route.Request.ResourceType == "image"
			? route.FulfillAsync(new RouteFulfillOptions { ContentType = "image/svg+xml", Body = PlaceholderArtwork })
			: route.ContinueAsync();

	internal static async Task<string> UnmatchedAsync() => await (await StackAsync()).UnmatchedAsync();

	internal static async Task<string> RequestJournalAsync() => await (await StackAsync()).RequestJournalAsync();

	internal static Task<IAsyncDisposable> StubAsync(string urlPath, int status, string body, string method = "GET") =>
		StubAsync(new StubSpec(urlPath, status, body) { Method = method });

	internal static async Task<IAsyncDisposable> StubAsync(StubSpec spec)
	{
		var stack = await StackAsync();
		lock (CaptureGate)
		{
			Stubbed.Add(spec.UrlPath);
		}

		var ids = new List<string>();
		foreach (var path in StubPaths(spec.UrlPath))
		{
			var response = await stack.WireMock.PostAsJsonAsync("/__admin/mappings", Mapping(spec, path));
			response.EnsureSuccessStatusCode();
			ids.Add(JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<string>());
		}

		return new StubRegistration(stack, ids);
	}

	private static JsonObject Mapping(StubSpec spec, string path)
	{
		var request = new JsonObject { ["method"] = spec.Method, ["urlPath"] = path };
		if (spec.Query.Count > 0)
		{
			var matchers = new JsonObject();
			foreach (var (key, value) in spec.Query)
			{
				matchers[key] = new JsonObject { ["equalTo"] = value };
			}

			request["queryParameters"] = matchers;
		}

		var headers = new JsonObject { ["Content-Type"] = "application/json" };
		foreach (var (key, value) in spec.Headers)
		{
			headers[key] = value;
		}

		var response = new JsonObject
		{
			["status"] = spec.Status,
			["headers"] = headers,
			["body"] = spec.Body,
		};
		if (spec.DelayMs > 0)
		{
			response["fixedDelayMilliseconds"] = spec.DelayMs;
		}

		return new JsonObject
		{
			["priority"] = 1,
			["request"] = request,
			["response"] = response,
		};
	}

	private static IEnumerable<string> StubPaths(string urlPath) =>
		IsLive ? ProviderRecording.AllEndpoints.Select(endpoint => $"{endpoint.Prefix}{urlPath}") : [urlPath];

	private sealed class StubRegistration(E2EStack stack, IReadOnlyList<string> ids) : IAsyncDisposable
	{
		public async ValueTask DisposeAsync()
		{
			foreach (var id in ids)
			{
				(await stack.WireMock.DeleteAsync($"/__admin/mappings/{id}")).EnsureSuccessStatusCode();
			}
		}
	}

	internal static async Task WaitForRequestsBeyondAsync(string urlPathSegment, int baseline, float timeout = 30_000)
	{
		var deadline = Environment.TickCount64 + (long)timeout;
		while (Environment.TickCount64 < deadline)
		{
			if (await RequestCountAsync(urlPathSegment) > baseline)
			{
				return;
			}

			await Task.Delay(250);
		}

		await Assert.That(await RequestCountAsync(urlPathSegment)).IsGreaterThan(baseline);
	}

	internal static async Task<int> RequestCountAsync(string urlPathSegment) =>
		(JsonNode.Parse(await RequestJournalAsync())?["requests"]?.AsArray() ?? []).Count(entry =>
			entry?["request"]?["url"]?.GetValue<string>()?.Contains(urlPathSegment, StringComparison.Ordinal) is true
		);

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
		if (!Boot.IsValueCreated || !Boot.Value.IsCompletedSuccessfully)
		{
			return;
		}

		var served = new List<string>();
		var unmatched = new List<string>();
		foreach (var stack in _stacks)
		{
			served.AddRange(Urls(await stack.RequestJournalAsync()));
			unmatched.AddRange(Urls(await stack.UnmatchedAsync()));
		}

		if (Environment.GetEnvironmentVariable("E2E_AUDIT") is "true")
		{
			await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "e2e-served.txt"), served.Distinct());
			await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "e2e-unmatched.txt"), unmatched.Distinct());
		}

		if (IsLive)
		{
			await CaptureRemainingAsync();
			if (Environment.GetEnvironmentVariable("E2E_PRUNE") is "true")
			{
				await PruneUncapturedFixturesAsync();
			}
		}

		await _browser!.DisposeAsync();
		_playwright!.Dispose();
		foreach (var stack in _stacks)
		{
			await stack.StopAsync();
		}

		Directory.Delete(_keyRing, recursive: true);

		List<string> crashed;
		lock (CaptureGate)
		{
			crashed = [.. CircuitFailures];
		}

		await Assert.That(unmatched.Distinct()).IsEmpty();
		await Assert.That(crashed).IsEmpty();
	}

	private static IEnumerable<string> Urls(string journalJson) =>
		(JsonNode.Parse(journalJson)?["requests"]?.AsArray() ?? [])
			.Select(entry => (entry?["request"] ?? entry)?["url"]?.GetValue<string>())
			.OfType<string>()
			.Distinct();

	private static async Task CaptureRemainingAsync()
	{
		var snapshots = new List<string>();
		foreach (var stack in _stacks)
		{
			snapshots.Add(await stack.SnapshotAsync());
		}

		var recorded = RecordedStubs.Write(RecordedStubs.Merge(snapshots), WireMockStubs.MappingsDirectory, IsStubbed).Where(FirstCapture).ToList();
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

	private static bool IsStubbed(string replayPath)
	{
		lock (CaptureGate)
		{
			return Stubbed.Contains(replayPath);
		}
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
		var policy = WireMockStubs.PolicyFor(response.Path);
		var problems = policy is FixturePolicy.Value ? ValueDrift(response.Body, committed) : JsonContract.Incompatibilities(response.Body, committed);

		var receivedPath = $"{fixturePath[..^".verified.json".Length]}.received.json";
		if (problems.Count > 0)
		{
			await WriteFixtureAsync(receivedPath, response.Body);
		}
		else
		{
			File.Delete(receivedPath);
			if (policy is FixturePolicy.Contract)
			{
				await WriteFixtureAsync(fixturePath, response.Body);
			}
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
			var slug = Path.GetFileNameWithoutExtension(mappingFile);
			if (provider == "mappings" || !capturedProviders.Contains(provider) || captured.Contains($"{provider}/{slug}"))
			{
				continue;
			}

			var mapped = JsonNode.Parse(await File.ReadAllTextAsync(mappingFile))?["request"]?["urlPath"]?.GetValue<string>();
			if (mapped is not null && WireMockStubs.PolicyFor(mapped) is FixturePolicy.Frozen)
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

	private static async Task<E2EStack> StackAsync() => (await LeaseAsync()).Stack;

	private static Task<Lease> LeaseAsync() => Leases.GetOrAdd(TestContext.Current!.Id, _ => new Lazy<Task<Lease>>(AcquireAsync)).Value;

	private static async Task<Lease> AcquireAsync()
	{
		await Boot.Value;
		return new Lease(await Idle.Reader.ReadAsync());
	}

	[AfterEvery(Test)]
	public static async Task ReleaseAsync(TestContext context)
	{
		if (!Leases.TryRemove(context.Id, out var pending) || !pending.IsValueCreated)
		{
			return;
		}

		var lease = await pending.Value;
		try
		{
			foreach (var browserContext in lease.Contexts)
			{
				await browserContext.CloseAsync();
			}
		}
		finally
		{
			Idle.Writer.TryWrite(lease.Stack);
		}
	}

	private sealed class Lease(E2EStack stack)
	{
		internal E2EStack Stack { get; } = stack;

		internal ConcurrentBag<IBrowserContext> Contexts { get; } = [];
	}

	private static async Task StartAsync()
	{
		var headed = Environment.GetEnvironmentVariable("E2E_HEADED") is "true";
		_playwright = await Playwright.CreateAsync();
		_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = !headed, SlowMo = headed ? 2000 : 0 });

		_keyRing = Directory.CreateTempSubdirectory("sammlarr-e2e-keys-").FullName;
		var seeds = Enumerable.Range(0, StackCount).Select(_ => E2EStack.Seed(IsLive, _keyRing)).ToList();
		_stacks = await Task.WhenAll(seeds.Select((seed, index) => E2EStack.LaunchAsync(seed, index, IsLive, RecordCircuitFailure)));
		foreach (var stack in _stacks)
		{
			Idle.Writer.TryWrite(stack);
		}
	}

	private static void RecordCircuitFailure(string message)
	{
		lock (CaptureGate)
		{
			CircuitFailures.Add(message);
		}
	}
}
