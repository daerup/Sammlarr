using Domain.Media;

namespace Core.Media;

internal sealed class IdentityMerge
{
	private readonly Lock _gate = new();
	private readonly Dictionary<IExternalMediaId, MediaIdentity> _building = [];

	internal void Merge(MediaIdentity preliminary, IReadOnlyDictionary<IExternalMediaId, MediaIdentity> persisted, MediaWriteBuffer buffer)
	{
		lock (_gate)
		{
			var touching = Matches(preliminary, persisted).Concat(Matches(preliminary, _building)).DistinctBy(match => match.Id).ToList();
			var anchor = touching.Append(preliminary).First();
			var canonical = anchor with { Keys = touching.Aggregate(preliminary.Keys, (all, match) => all + match.Keys) };

			foreach (var external in canonical.Keys)
			{
				_building[external] = canonical;
			}

			foreach (var absorbed in touching.Where(match => match.Id != canonical.Id))
			{
				buffer.MergeIdentity(absorbed.Id, canonical);
			}

			buffer.AddIdentity(canonical);
		}
	}

	internal MediaIdentity Canonical(MediaIdentity preliminary)
	{
		lock (_gate)
		{
			return preliminary.Keys.FirstOrNone().Map(external => _building[external]).GetValueOr(preliminary);
		}
	}

	private static IEnumerable<MediaIdentity> Matches(MediaIdentity preliminary, IReadOnlyDictionary<IExternalMediaId, MediaIdentity> index)
	{
		return preliminary.Keys.Select(index.GetValueOrNone).WhereSome();
	}
}
