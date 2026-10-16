using CsCheck;
using Domain.Media;

namespace Domain.Tests.Media;

public class ClaimRuleLaws
{
	private static readonly MediaId[] Rows = [.. Enumerable.Range(1, 4).Select(index => MediaId.From(new Guid(index, 0, 0, new byte[8])))];
	private static readonly MediaId Fresh = MediaId.From(new Guid(9, 0, 0, new byte[8]));
	private static readonly Gen<IIdentityKey> Keys = Gen.Select(Gen.Int[0, 2], Gen.Int[1, 4], Key);
	private static readonly Gen<Dictionary<IIdentityKey, MediaId>> Libraries = Gen.Select(Keys, Gen.Int[0, Rows.Length - 1]).Array[0, 8].Select(Library);
	private static readonly Gen<IdentityClaim> Claims = Gen.Select(Keys.Array[1, 4], Gen.Int[-1, Rows.Length - 1]).Select(Claim);

	[Test]
	public void Decide_ForAnyLibrary_NeverGivesOneKeyToTwoRows()
	{
		Gen.Select(Libraries, Claims).Sample((owners, claim) => GivesEachKeyOneRow(owners, ClaimRule.Decide(claim, owners, Held(owners), Fresh)));
	}

	[Test]
	public void Decide_ForAnyLibrary_NeverHoldsTwoKeysOfOneSourceOnARow()
	{
		Gen.Select(Libraries, Claims).Sample((owners, claim) => HoldsOneKeyPerSource(ClaimRule.Decide(claim, owners, Held(owners), Fresh)));
	}

	[Test]
	public void Decide_ForAnyClaim_DisputesExactlyTheKeysAPinnedClaimLeavesToAnotherRow()
	{
		Gen.Select(Libraries, Claims).Sample((owners, claim) => DisputesWhatOthersOwn(owners, claim, ClaimRule.Decide(claim, owners, Held(owners), Fresh)));
	}

	private static bool DisputesWhatOthersOwn(Dictionary<IIdentityKey, MediaId> owners, IdentityClaim claim, ClaimOutcome outcome)
	{
		return outcome switch
		{
			Claimed claimed => claimed.Disputed.SetEquals(LeftOut(owners, claim, claimed)),
			Conflicted => true,
		};
	}

	private static IEnumerable<Disputed> LeftOut(Dictionary<IIdentityKey, MediaId> owners, IdentityClaim claim, Claimed claimed)
	{
		return claim.Pinned.Match(
			_ => claim.Keys.Where(key => !claimed.Keys.Contains(key)).Select(key => owners.GetValueOrNone(key).Map(owner => new Disputed(key, owner))).WhereSome(),
			() => []
		);
	}

	private static bool GivesEachKeyOneRow(Dictionary<IIdentityKey, MediaId> owners, ClaimOutcome outcome)
	{
		return outcome switch
		{
			Claimed claimed => claimed.Keys.All(key => owners.GetValueOrNone(key).Match(owner => owner == claimed.Id, () => true)),
			Conflicted => true,
		};
	}

	private static bool HoldsOneKeyPerSource(ClaimOutcome outcome)
	{
		return outcome switch
		{
			Claimed claimed => claimed.Keys.GroupBy(key => key.Source).All(source => source.Count() == 1),
			Conflicted => true,
		};
	}

	private static IIdentityKey Key(int source, int value)
	{
		return source switch
		{
			0 => TmdbId.From($"{value}", MediaType.Movie),
			1 => ImdbId.From($"tt{value}", MediaType.Movie),
			_ => TraktId.From($"{value}", MediaType.Movie),
		};
	}

	private static Dictionary<IIdentityKey, MediaId> Library((IIdentityKey Key, int Row)[] entries)
	{
		Dictionary<IIdentityKey, MediaId> owners = [];
		foreach (var (key, row) in entries)
		{
			var taken = owners.Where(owner => owner.Value == Rows[row]).Any(owner => owner.Key.Source == key.Source);
			if (!taken)
			{
				owners.TryAdd(key, Rows[row]);
			}
		}

		return owners;
	}

	private static Dictionary<MediaId, ValueSet<IIdentityKey>> Held(Dictionary<IIdentityKey, MediaId> owners)
	{
		return owners.GroupBy(owner => owner.Value).ToDictionary(row => row.Key, row => (ValueSet<IIdentityKey>)[.. row.Select(owner => owner.Key)]);
	}

	private static IdentityClaim Claim((IIdentityKey[] Keys, int Pinned) claim)
	{
		return new([.. claim.Keys], Optional.When(claim.Pinned >= 0, () => Rows[claim.Pinned]), []);
	}
}
