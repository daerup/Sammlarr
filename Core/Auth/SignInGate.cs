using System.Collections.Concurrent;
using Domain.Identity;

namespace Core.Auth;

internal sealed class SignInGate
{
	private readonly ConcurrentDictionary<UserId, SemaphoreSlim> _gates = new();

	internal async Task<IDisposable> EnterAsync(UserId userId, CancellationToken cancellationToken = default)
	{
		var gate = _gates.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
		await gate.WaitAsync(cancellationToken);
		return new Lease(gate);
	}

	private sealed class Lease(SemaphoreSlim gate) : IDisposable
	{
		public void Dispose() => gate.Release();
	}
}
