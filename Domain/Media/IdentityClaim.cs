using Domain.Providers;

namespace Domain.Media;

public readonly record struct IdentityClaim(ValueSet<IIdentityKey> Keys, Optional<MediaId> Pinned, ValueSet<RemoteService> Sources);

public static class ClaimRule
{
	public static ClaimOutcome Decide(
		IdentityClaim claim,
		IReadOnlyDictionary<IIdentityKey, MediaId> owners,
		IReadOnlyDictionary<MediaId, ValueSet<IIdentityKey>> held,
		MediaId fresh
	)
	{
		return claim.Pinned.Match(pinned => Extend(pinned, claim.Keys, owners, held), () => Identify(claim.Keys, owners, held, fresh));
	}

	private static ClaimOutcome Identify(
		ValueSet<IIdentityKey> keys,
		IReadOnlyDictionary<IIdentityKey, MediaId> owners,
		IReadOnlyDictionary<MediaId, ValueSet<IIdentityKey>> held,
		MediaId fresh
	)
	{
		List<MediaId> rows = [.. keys.Select(owners.GetValueOrNone).WhereSome().Distinct()];
		var row = rows.FirstOrNone().GetValueOr(fresh);
		var existing = held.GetValueOrNone(row).GetValueOr([]);
		if (rows.Count > 1)
		{
			return new Conflicted([.. rows], [.. keys.Where(owners.ContainsKey)]);
		}

		List<IIdentityKey> clashing = [.. keys.Where(key => Clashes(key, keys, existing) || !OfOneType(key, keys, existing))];
		return clashing.Count > 0 ? new Conflicted([.. rows], [.. clashing]) : new Claimed(row, existing + keys);
	}

	private static ClaimOutcome Extend(
		MediaId pinned,
		ValueSet<IIdentityKey> keys,
		IReadOnlyDictionary<IIdentityKey, MediaId> owners,
		IReadOnlyDictionary<MediaId, ValueSet<IIdentityKey>> held
	)
	{
		return held.GetValueOrNone(pinned)
			.Match<ValueSet<IIdentityKey>, ClaimOutcome>(existing => Extended(pinned, keys, owners, existing), () => new Conflicted([], keys));
	}

	private static Claimed Extended(
		MediaId pinned,
		ValueSet<IIdentityKey> keys,
		IReadOnlyDictionary<IIdentityKey, MediaId> owners,
		ValueSet<IIdentityKey> existing
	)
	{
		var kept = keys.Where(key =>
			owners.GetValueOrNone(key).Match(owner => owner == pinned, () => true) && !Clashes(key, keys, existing) && OfRowType(key, existing)
		);
		return new Claimed(pinned, existing + kept);
	}

	private static bool OfRowType(IIdentityKey key, ValueSet<IIdentityKey> existing)
	{
		return existing.All(held => held.Type == key.Type);
	}

	private static bool Clashes(IIdentityKey key, IEnumerable<IIdentityKey> claimed, IEnumerable<IIdentityKey> existing)
	{
		return claimed.Concat(existing).Any(other => other.Source == key.Source && other.Value != key.Value);
	}

	private static bool OfOneType(IIdentityKey key, IEnumerable<IIdentityKey> claimed, IEnumerable<IIdentityKey> existing)
	{
		return existing.Concat(claimed).All(other => other.Type == key.Type);
	}
}
