using System.Collections.Concurrent;
using Contracts.Identity;
using Domain.Identity;

namespace UI.Shared.Identity;

internal sealed class CircuitUserRegistry : ISessionInvalidator
{
	private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Action>> signOutsByUser = new();

	public IDisposable Register(UserId userId, Action signOut)
	{
		var token = Guid.NewGuid();
		signOutsByUser.GetOrAdd(userId.Value, _ => new ConcurrentDictionary<Guid, Action>())[token] = signOut;
		return new Registration(this, userId.Value, token);
	}

	public void Invalidate(UserId userId)
	{
		if (signOutsByUser.TryGetValue(userId.Value, out var signOuts))
		{
			foreach (var signOut in signOuts.Values)
			{
				signOut();
			}
		}
	}

	private void Unregister(Guid userId, Guid token)
	{
		if (signOutsByUser.TryGetValue(userId, out var signOuts))
		{
			signOuts.TryRemove(token, out _);
			if (signOuts.IsEmpty)
			{
				signOutsByUser.TryRemove(userId, out _);
			}
		}
	}

	private sealed class Registration(CircuitUserRegistry registry, Guid userId, Guid token) : IDisposable
	{
		public void Dispose() => registry.Unregister(userId, token);
	}
}
