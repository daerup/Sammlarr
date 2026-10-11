using Contracts.Identity;
using Contracts.Providers;
using Domain.Providers;
using Shared.Concurrency;

namespace API.Http;

internal sealed class RemoteCapacity(Lanes.Lanes lanes, ICurrentUser user) : IRemoteCapacity
{
	public RemoteLoad Of(RemoteService remote)
	{
		var loads = lanes.Loads(remote, Optional.When(user.IsAuthenticated, () => user.Id), LanePriority.Current.Class).ToList();
		var settings = lanes.Settings(remote);
		return new RemoteLoad(
			loads.Select(load => load.BlockedUntil).WhereSome().OrderDescending().FirstOrNone(),
			loads.Select(load => load.EstimatedWait).DefaultIfEmpty(TimeSpan.Zero).Max(),
			loads.Any(load => load.Down),
			loads.Sum(load => load.Waiting),
			settings.Map(found => found.MaxWait).GetValueOr(TimeSpan.Zero),
			settings.Map(found => found.EnrichWait).GetValueOr(TimeSpan.Zero)
		)
		{
			Paced = loads.Select(load => load.Paced).WhereSome().OrderByDescending(pace => pace.OpensAt).FirstOrNone(),
		};
	}
}
