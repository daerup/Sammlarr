using System.Collections.Concurrent;
using System.Threading.Channels;
using E2E.Tests.Recording;
using Microsoft.Playwright;

namespace E2E.Tests.SelfHosted;

public static class SelfHostedApp
{
	internal static int StackCount { get; } = Math.Clamp(Environment.ProcessorCount / 8, 1, 2);

	private static readonly TimeSpan LeaseTimeout = TimeSpan.FromMinutes(5);
	private static readonly Lazy<Task> Boot = new(StartAsync);
	private static readonly Lock Gate = new();
	private static readonly List<JournalEntry> Journal = [];
	private static readonly List<string> CircuitFailures = [];
	private static readonly ConcurrentDictionary<string, Lazy<Lease>> Leases = [];
	private static readonly Channel<SelfHostedStack> Idle = Channel.CreateUnbounded<SelfHostedStack>();

	private static SelfHostedStack[] _stacks = [];
	private static string _sessionDir = "";
	private static IPlaywright? _playwright;
	private static IBrowser? _browser;

	internal static Task<SelfHostedStack> StackAsync() => Current().Ready;

	internal static async Task<IPage> OpenPageAsync()
	{
		var lease = Current();
		var stack = await lease.Ready;
		var context = await BrowserPages.OpenAsync(_browser!, stack.BaseUrl);
		lease.Contexts.Add(context);
		return await context.NewPageAsync();
	}

	private static Lease Current() => Leases.GetOrAdd(TestContext.Current!.Id, _ => new Lazy<Lease>(Take)).Value;

	private static Lease Take()
	{
		var taken = TakeAsync();
		return new Lease(taken, ReadyAsync(taken));
	}

	private static async Task<SelfHostedStack> TakeAsync()
	{
		await Boot.Value;
		using var timeout = new CancellationTokenSource(LeaseTimeout);
		return await Idle.Reader.ReadAsync(timeout.Token);
	}

	private static async Task<SelfHostedStack> ReadyAsync(Task<SelfHostedStack> taken)
	{
		var stack = await taken;
		Record(await stack.ReadyAsync());
		return stack;
	}

	[AfterEvery(Test)]
	public static async Task ReleaseAsync(TestContext context)
	{
		if (!Leases.TryRemove(context.Id, out var lease) || !lease.IsValueCreated)
		{
			return;
		}

		var taken = lease.Value.Taken;
		try
		{
			await FailureSnapshot.WriteAsync(context, lease.Value.Contexts);
			foreach (var browserContext in lease.Value.Contexts)
			{
				await browserContext.CloseAsync();
			}
		}
		finally
		{
			await ((Task)taken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
			if (taken.IsCompletedSuccessfully)
			{
				Idle.Writer.TryWrite(taken.Result);
			}
		}
	}

	[After(TestSession)]
	public static async Task StopAsync()
	{
		if (!Boot.IsValueCreated || !Boot.Value.IsCompletedSuccessfully)
		{
			return;
		}

		foreach (var stack in _stacks)
		{
			Record(await stack.JournalAsync());
		}

		await _browser!.DisposeAsync();
		_playwright!.Dispose();
		foreach (var stack in _stacks)
		{
			await stack.StopAsync();
		}

		List<JournalEntry> journal;
		lock (Gate)
		{
			journal = [.. Journal.Distinct()];
		}

		List<string> crashed;
		lock (Gate)
		{
			crashed = [.. CircuitFailures];
		}

		await Assert.That(E2EApp.IsLive ? await RecordAsync(journal) : Unmatched(journal)).IsEmpty();
		CircuitFailureSink.AssertNone(crashed);
		Directory.Delete(_sessionDir, recursive: true);
	}

	private static IReadOnlyList<string> Unmatched(IEnumerable<JournalEntry> journal)
	{
		return [.. journal.Where(entry => !entry.Matched).Select(entry => entry.Url).Distinct()];
	}

	private static async Task<IReadOnlyList<string>> RecordAsync(IReadOnlyList<JournalEntry> journal)
	{
		var unrecordable = journal.Where(entry => MetadataRecorder.Find(entry.Method, entry.Url) is null);
		var recordables = journal.Select(entry => MetadataRecorder.Find(entry.Method, entry.Url)).OfType<Recordable>().Distinct();
		return [.. Unmatched(unrecordable), .. await MetadataRecorder.RecordAsync(recordables)];
	}

	private static void RecordCircuitFailure(string message)
	{
		lock (Gate)
		{
			CircuitFailures.Add(message);
		}
	}

	private static void Record(IReadOnlyList<JournalEntry> journal)
	{
		lock (Gate)
		{
			Journal.AddRange(journal);
		}
	}

	private static async Task StartAsync()
	{
		_playwright = await Playwright.CreateAsync();
		_browser = await BrowserPages.LaunchAsync(_playwright);
		_sessionDir = Directory.CreateTempSubdirectory("sammlarr-selfhosted-").FullName;
		var certificates = await TestCertificates.CreateAsync(MetadataHosts.All, _sessionDir);
		var keyRing = Directory.CreateDirectory(Path.Combine(_sessionDir, "keys")).FullName;
		_stacks = await Task.WhenAll(
			Enumerable
				.Range(0, StackCount)
				.Select(index =>
					SelfHostedStack.LaunchAsync(
						certificates,
						keyRing,
						Directory.CreateDirectory(Path.Combine(_sessionDir, $"stack-{index}")).FullName,
						index,
						E2EApp.IsLive,
						RecordCircuitFailure
					)
				)
		);
		foreach (var stack in _stacks)
		{
			Idle.Writer.TryWrite(stack);
		}
	}

	private sealed record Lease(Task<SelfHostedStack> Taken, Task<SelfHostedStack> Ready)
	{
		internal ConcurrentBag<IBrowserContext> Contexts { get; } = [];
	}
}
