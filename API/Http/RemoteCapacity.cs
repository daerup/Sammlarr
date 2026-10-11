using System.Collections.Concurrent;
using Contracts;
using Contracts.Providers;
using Domain.Providers;

namespace API.Http;

internal sealed class RemoteCapacity(Lanes.Lanes lanes, IDateTimeProvider clock) : IRemoteCapacity
{
	private readonly ConcurrentDictionary<RemoteService, DateTimeOffset> _blockedUntil = new();

	public RemoteLoad Of(RemoteService remote)
	{
		var loads = lanes.Loads(remote, new None()).ToList();
		var settings = lanes.Settings(remote);
		var blocked = loads.Select(load => load.BlockedUntil).Append(_blockedUntil.GetValueOrNone(remote)).WhereSome().OrderDescending().FirstOrNone();
		return new RemoteLoad(
			blocked,
			loads.Select(load => load.Waiting / (double)Math.Max(1, load.InFlight + load.Waiting)).DefaultIfEmpty(0).Max(),
			settings.Map(found => found.MaxWait).GetValueOr(TimeSpan.Zero),
			settings.Map(found => found.EnrichWait).GetValueOr(TimeSpan.Zero)
		);
	}

	internal void Block(RemoteService remote, TimeSpan duration)
	{
		var until = clock.Now + duration;
		_blockedUntil.AddOrUpdate(remote, until, (_, current) => current > until ? current : until);
	}
}
