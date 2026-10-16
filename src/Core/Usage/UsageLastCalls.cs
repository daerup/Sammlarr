using System.Collections.Concurrent;
using Contracts.Usage;
using Domain.Providers;

namespace Core.Usage;

internal sealed class UsageLastCalls : IUsageLastCalls
{
	private readonly ConcurrentDictionary<RemoteService, DateTimeOffset> _last = new();

	public Optional<DateTimeOffset> Of(RemoteService service) => _last.GetValueOrNone(service);

	public void Saw(RemoteService service, DateTimeOffset at) => _last.AddOrUpdate(service, at, (_, seen) => seen > at ? seen : at);
}
