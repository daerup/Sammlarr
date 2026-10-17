using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using E2E.Tests.Recording;
using Microsoft.Playwright;

namespace E2E.Tests;

public static class E2EApp
{
	internal static bool IsLive { get; } = Environment.GetEnvironmentVariable("E2E_LIVE") is "true";

	internal static int StackCount { get; } = IsLive ? 1 : Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

	private static readonly Lazy<Task> Boot = new(StartAsync);
	private static readonly Lock CaptureGate = new();
	private static readonly HashSet<string> Stubbed = [];
	private static readonly List<string> CircuitFailures = [];
	private static readonly ConcurrentDictionary<string, Lazy<Task<Lease>>> Leases = [];
	private static readonly Channel<E2EStack> Idle = Channel.CreateUnbounded<E2EStack>();

	private static E2EStack[] _stacks = [];
	private static IPlaywright? _playwright;
	private static IBrowser? _browser;
	private static string _keyRing = "";

	internal static Task<IPage> OpenPageAsync() => NewPageAsync(_ => null);

	internal static Task<IPage> OpenAuthenticatedPageAsync() => NewPageAsync(stack => stack.ForgeAdminCookie());

	internal static Task<IPage> OpenMemberPageAsync() => NewPageAsync(stack => stack.ForgeMemberCookie());

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
		var context = await BrowserPages.OpenAsync(_browser!, lease.Stack.BaseUrl);
		lease.Contexts.Add(context);
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

	private static async Task<string> RequestJournalAsync() => await (await StackAsync()).RequestJournalAsync();

	internal static async Task<Uri> WireMockUrlAsync() => (await StackAsync()).WireMockUrl;

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

		if (spec.BodyContains.Count > 0)
		{
			request["bodyPatterns"] = new JsonArray([.. spec.BodyContains.Select(text => (JsonNode)new JsonObject { ["contains"] = text })]);
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
			|| entry?["request"]?["body"]?.GetValue<string>()?.Contains(urlPathSegment, StringComparison.Ordinal) is true
		);

	internal static async Task<string> ServedBodyAsync(string urlPath)
	{
		var served = (JsonNode.Parse(await RequestJournalAsync())?["requests"]?.AsArray() ?? []).First(entry =>
			entry?["request"]?["url"]?.GetValue<string>()?.Split('?')[0].EndsWith(urlPath, StringComparison.Ordinal) is true
		);
		return served!["response"]!["body"]!.GetValue<string>();
	}

	[After(TestSession)]
	public static async Task StopAsync()
	{
		if (!Boot.IsValueCreated || !Boot.Value.IsCompletedSuccessfully)
		{
			return;
		}

		var served = new List<string>();
		var unmatched = new List<string>();
		try
		{
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
			}
		}
		finally
		{
			await _browser!.DisposeAsync();
			_playwright!.Dispose();
			foreach (var stack in _stacks)
			{
				await stack.StopAsync();
			}

			Directory.Delete(_keyRing, recursive: true);
		}

		List<string> crashed;
		lock (CaptureGate)
		{
			crashed = [.. CircuitFailures];
		}

		await Assert.That(unmatched.Distinct()).IsEmpty();
		CircuitFailureSink.AssertNone(crashed);
	}

	[After(TestSession, Order = 1)]
	public static async Task PruneAsync()
	{
		if (IsLive && Environment.GetEnvironmentVariable("E2E_PRUNE") is "true")
		{
			await FixtureFiles.PruneAsync();
		}
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

		var recorded = RecordedStubs.Write(RecordedStubs.Merge(snapshots), WireMockStubs.MappingsDirectory, IsStubbed).Where(FixtureFiles.Capture).ToList();
		await Assert.That(await FixtureFiles.StoreAsync(recorded)).IsEmpty();
	}

	private static bool IsStubbed(string replayPath)
	{
		lock (CaptureGate)
		{
			return Stubbed.Contains(replayPath);
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
			await FailureSnapshot.WriteAsync(context, lease.Contexts);
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
		_playwright = await Playwright.CreateAsync();
		_browser = await BrowserPages.LaunchAsync(_playwright);

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
