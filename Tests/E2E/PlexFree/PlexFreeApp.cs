using System.Collections.Concurrent;
using System.Threading.Channels;

namespace E2E.Tests.PlexFree;

public static class PlexFreeApp
{
	internal const int StackCount = 2;

	private static readonly TimeSpan LeaseTimeout = TimeSpan.FromMinutes(5);
	private static readonly Lazy<Task> Boot = new(StartAsync);
	private static readonly Lock Gate = new();
	private static readonly List<string> Unmatched = [];
	private static readonly ConcurrentDictionary<string, Lazy<Lease>> Leases = [];
	private static readonly Channel<PlexFreeStack> Idle = Channel.CreateUnbounded<PlexFreeStack>();

	private static PlexFreeStack[] _stacks = [];
	private static string _sessionDir = "";

	internal static Task<PlexFreeStack> StackAsync() => Leases.GetOrAdd(TestContext.Current!.Id, _ => new Lazy<Lease>(Take)).Value.Ready;

	private static Lease Take()
	{
		var taken = TakeAsync();
		return new Lease(taken, ReadyAsync(taken));
	}

	private static async Task<PlexFreeStack> TakeAsync()
	{
		await Boot.Value;
		using var timeout = new CancellationTokenSource(LeaseTimeout);
		return await Idle.Reader.ReadAsync(timeout.Token);
	}

	private static async Task<PlexFreeStack> ReadyAsync(Task<PlexFreeStack> taken)
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
		await ((Task)taken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		if (taken.IsCompletedSuccessfully)
		{
			Idle.Writer.TryWrite(taken.Result);
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
			Record(await stack.UnmatchedAsync());
		}

		foreach (var stack in _stacks)
		{
			await stack.StopAsync();
		}

		List<string> unmatched;
		lock (Gate)
		{
			unmatched = [.. Unmatched.Distinct()];
		}

		await Assert.That(unmatched).IsEmpty();
		Directory.Delete(_sessionDir, recursive: true);
	}

	private static void Record(IReadOnlyList<string> unmatched)
	{
		lock (Gate)
		{
			Unmatched.AddRange(unmatched);
		}
	}

	private static async Task StartAsync()
	{
		_sessionDir = Directory.CreateTempSubdirectory("sammlarr-plexfree-").FullName;
		var certificates = await TestCertificates.CreateAsync(MetadataHosts.All, _sessionDir);
		var keyRing = Directory.CreateDirectory(Path.Combine(_sessionDir, "keys")).FullName;
		_stacks = await Task.WhenAll(
			Enumerable
				.Range(0, StackCount)
				.Select(index => PlexFreeStack.LaunchAsync(certificates, keyRing, Directory.CreateDirectory(Path.Combine(_sessionDir, $"stack-{index}")).FullName))
		);
		foreach (var stack in _stacks)
		{
			Idle.Writer.TryWrite(stack);
		}
	}

	private sealed record Lease(Task<PlexFreeStack> Taken, Task<PlexFreeStack> Ready);
}
