using Domain.Media;
using TestInfrastructure;

namespace Domain.Tests.Media;

public class ClaimRuleTests
{
	private static readonly MediaId Stored = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Other = MediaId.From(new Guid(2, 0, 0, new byte[8]));
	private static readonly MediaId Fresh = MediaId.From(new Guid(3, 0, 0, new byte[8]));

	[Test]
	public async Task Decide_ForKeysNoRowHolds_ClaimsAFreshRow()
	{
		var outcome = ClaimRule.Decide(Claim([Tmdb("15739")], new None()), Owners(), Held(), Fresh);

		var claimed = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Claimed>();
		await Assert.That(claimed!.Id).IsEqualTo(Fresh);
	}

	[Test]
	public async Task Decide_ForKeysOnTwoStoredRows_IsAConflictAndMergesNothing()
	{
		var outcome = ClaimRule.Decide(
			Claim([Tmdb("15739"), Imdb("tt0207972")], new None()),
			Owners((Tmdb("15739"), Stored), (Imdb("tt0207972"), Other)),
			Held((Stored, [Tmdb("15739")]), (Other, [Imdb("tt0207972")])),
			Fresh
		);

		var conflicted = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Conflicted>();
		await Assert.That(conflicted!.Owners).IsEquivalentTo([Stored, Other]);
	}

	[Test]
	public async Task Decide_ForASecondTmdbIdOnAStoredRow_IsAConflict()
	{
		var outcome = ClaimRule.Decide(
			Claim([Tmdb("26177"), Imdb("tt0083564")], new None()),
			Owners((Imdb("tt0083564"), Stored)),
			Held((Stored, [Tmdb("15739"), Imdb("tt0083564")])),
			Fresh
		);

		await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Conflicted>();
	}

	[Test]
	public async Task Decide_ForAPinnedTitleWhoseEnricherNamesAnotherRowsKey_DropsThatKeyAndKeepsTheId()
	{
		var outcome = ClaimRule.Decide(
			Claim([Imdb("tt0207972"), Trakt("9600")], Some.Of(Stored)),
			Owners((Imdb("tt0207972"), Other)),
			Held((Stored, [Tmdb("15739")]), (Other, [Imdb("tt0207972")])),
			Fresh
		);

		var claimed = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Claimed>();
		await Assert.That(claimed!.Id).IsEqualTo(Stored);
		await Assert.That(claimed.Keys).IsEquivalentTo([Tmdb("15739"), Trakt("9600")]);
	}

	[Test]
	public async Task Decide_ForAStoredRowsKeyAndANewOne_ExtendsThatRow()
	{
		var outcome = ClaimRule.Decide(
			Claim([Tmdb("15739"), Imdb("tt0083564")], new None()),
			Owners((Tmdb("15739"), Stored)),
			Held((Stored, [Tmdb("15739")])),
			Fresh
		);

		var claimed = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Claimed>();
		await Assert.That(claimed!.Id).IsEqualTo(Stored);
		await Assert.That(claimed.Keys).IsEquivalentTo([Tmdb("15739"), Imdb("tt0083564")]);
	}

	[Test]
	public async Task Decide_ForAPinnedTitleAndAKeyOfAnotherMediaType_DropsOnlyThatKey()
	{
		var showKey = TvdbId.From("81189", MediaType.Show);

		var outcome = ClaimRule.Decide(Claim([Imdb("tt0083564"), showKey], Some.Of(Stored)), Owners(), Held((Stored, [Tmdb("15739")])), Fresh);

		var claimed = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Claimed>();
		await Assert.That(claimed!.Keys).IsEquivalentTo([Tmdb("15739"), Imdb("tt0083564")]);
	}

	[Test]
	public async Task Decide_ForAPinnedTitleNoLongerStored_IsAConflictAndWritesNothing()
	{
		var outcome = ClaimRule.Decide(Claim([Imdb("tt0083564")], Some.Of(Stored)), Owners(), Held(), Fresh);

		var conflicted = await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Conflicted>();
		await Assert.That(conflicted!.Owners).IsEmpty();
	}

	[Test]
	public async Task Decide_ForAClaimCarryingTwoTmdbIds_IsAConflict()
	{
		var outcome = ClaimRule.Decide(Claim([Tmdb("15739"), Tmdb("26177")], new None()), Owners(), Held(), Fresh);

		await Assert.That(outcome).IsOfUnionType<ClaimOutcome, Conflicted>();
	}

	private static IdentityClaim Claim(ValueSet<IIdentityKey> keys, Optional<MediaId> pinned) => new(keys, pinned, []);

	private static IIdentityKey Tmdb(string value) => TmdbId.From(value, MediaType.Movie);

	private static IIdentityKey Imdb(string value) => ImdbId.From(value, MediaType.Movie);

	private static IIdentityKey Trakt(string value) => TraktId.From(value, MediaType.Movie);

	private static Dictionary<IIdentityKey, MediaId> Owners(params (IIdentityKey Key, MediaId Row)[] owners)
	{
		return owners.ToDictionary(entry => entry.Key, entry => entry.Row);
	}

	private static Dictionary<MediaId, ValueSet<IIdentityKey>> Held(params (MediaId Row, ValueSet<IIdentityKey> Keys)[] held)
	{
		return held.ToDictionary(entry => entry.Row, entry => entry.Keys);
	}
}
