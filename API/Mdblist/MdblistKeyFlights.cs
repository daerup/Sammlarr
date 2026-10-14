using System.Collections.Concurrent;
using Domain.Auth;

namespace API.Mdblist;

internal sealed class MdblistKeyFlights
{
	private readonly ConcurrentDictionary<string, Lazy<Task<AccessCheck>>> _flights = new();

	public Task<AccessCheck> JoinAsync(string apiKey, Func<Task<AccessCheck>> check)
	{
		return _flights.GetOrAdd(apiKey, _ => new Lazy<Task<AccessCheck>>(() => FlyAsync(apiKey, check))).Value;
	}

	private async Task<AccessCheck> FlyAsync(string apiKey, Func<Task<AccessCheck>> check)
	{
		try
		{
			return await check();
		}
		finally
		{
			_flights.TryRemove(apiKey, out _);
		}
	}
}
