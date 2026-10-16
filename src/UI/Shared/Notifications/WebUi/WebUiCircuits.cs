using System.Collections.Concurrent;
using Domain.Identity;
using UI.Shared.Services;

namespace UI.Shared.Notifications.WebUi;

internal sealed class WebUiCircuits
{
	private readonly ConcurrentDictionary<Guid, (UserId User, Action<Toast> Show)> circuits = new();

	public IDisposable Register(UserId user, Action<Toast> show)
	{
		var token = Guid.NewGuid();
		circuits[token] = (user, show);
		return new Registration(() => circuits.TryRemove(token, out _));
	}

	public void Show(UserId user, Toast toast)
	{
		foreach (var circuit in circuits.Values.Where(circuit => circuit.User == user))
		{
			circuit.Show(toast);
		}
	}

	private sealed class Registration(Action unregister) : IDisposable
	{
		public void Dispose() => unregister();
	}
}
