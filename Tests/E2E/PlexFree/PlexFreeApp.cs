using System.Collections.Concurrent;
using System.Threading.Channels;
using E2E.Tests.Recording;

namespace E2E.Tests.PlexFree;

public static class PlexFreeApp
{
	internal const int StackCount = 2;

	private static readonly TimeSpan LeaseTimeout = TimeSpan.FromMinutes(5);
	private static readonly Lazy<Task> Boot = new(StartAsync);
	private static readonly Lock Gate = new();
	private static readonly List<JournalEntry> Journal = [];
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
			Record(await stack.JournalAsync());
		}

		foreach (var stack in _stacks)
		{
			await stack.StopAsync();
		}

		List<JournalEntry> journal;
		lock (Gate)
		{
			journal = [.. Journal.Distinct()];
		}

		await Assert.That(E2EApp.IsLive ? await RecordAsync(journal) : Unmatched(journal)).IsEmpty();
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

	private static void Record(IReadOnlyList<JournalEntry> journal)
	{
		lock (Gate)
		{
			Journal.AddRange(journal);
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
				.Select(index =>
					PlexFreeStack.LaunchAsync(certificates, keyRing, Directory.CreateDirectory(Path.Combine(_sessionDir, $"stack-{index}")).FullName, E2EApp.IsLive)
				)
		);
		foreach (var stack in _stacks)
		{
			Idle.Writer.TryWrite(stack);
		}
	}

	private sealed record Lease(Task<PlexFreeStack> Taken, Task<PlexFreeStack> Ready);
}
