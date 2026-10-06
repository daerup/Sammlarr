using Domain.Wantlist.Arr;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Wantlist.Arr;

public class ArrTagSchemeTests
{
	private static readonly Guid GuestId = Guid.Parse("8f14e45f-ceea-467a-9575-6c0f6e3b4c11");

	private static readonly Guid FeedId = Guid.Parse("1679091c-5a88-4faf-afb5-e6087eb1b2dc");

	private static ArrTagScheme Testee => ArrTagScheme.Default;

	[Test]
	public async Task UserTag_ForAName_MarksItAsAUsersTag()
	{
		await Assert.That(Testee.UserTag(new ArrTagUser(GuestId, "Guest"))).IsEqualTo("sammlarr-u-guest");
	}

	[Test]
	public async Task UserTag_WhenTheNameHidesAZeroWidthCharacter_DropsIt()
	{
		await Assert.That(Testee.UserTag(new ArrTagUser(GuestId, "Gu​est"))).IsEqualTo("sammlarr-u-guest");
	}

	[Test]
	public async Task UserTag_ForAccentsSpacesAndPunctuation_KeepsOnlyLettersDigitsAndSingleHyphens()
	{
		await Assert.That(Testee.UserTag(new ArrTagUser(GuestId, "  Zoë Müller -- Smith!  "))).IsEqualTo("sammlarr-u-zoe-muller-smith");
	}

	[Test]
	public async Task UserTag_WhenNothingOfTheNameIsUsable_FallsBackToTheId()
	{
		await Assert.That(Testee.UserTag(new ArrTagUser(GuestId, "🎬"))).IsEqualTo("sammlarr-u-8f14e45f");
	}

	[Test]
	public async Task UserTag_WhenTheSchemeTakesTheId_IgnoresTheName()
	{
		var scheme = Testee with { UserSource = ArrUserTagSource.Id };

		await Assert.That(scheme.UserTag(new ArrTagUser(GuestId, "Guest"))).IsEqualTo("sammlarr-u-8f14e45f");
	}

	[Test]
	public async Task FeedTag_ForAVeryLongListName_KeepsItWhole()
	{
		var name = string.Join(" ", Enumerable.Repeat("Latest movies on digital release", 8));

		var tag = Testee.FeedTag(new ArrTagFeed(FeedId, name));

		await Assert.That(tag).StartsWith("sammlarr-latest-movies-on-digital-release-latest");
		await Assert.That(tag.Length).IsEqualTo("sammlarr-".Length + 8 * "latest-movies-on-digital-release".Length + 7);
	}

	[Test]
	public async Task FeedTag_WhenTheSchemeTakesTheId_UsesItsFirstEightCharacters()
	{
		var scheme = Testee with { FeedSource = ArrFeedTagSource.Id };

		await Assert.That(scheme.FeedTag(new ArrTagFeed(FeedId, "Docs"))).IsEqualTo("sammlarr-1679091c");
	}

	[Test]
	public async Task Labels_ForAFeedsTitleWithEveryPartOn_ListsThePrefixTheUserAndTheFeed()
	{
		var scheme = Testee with { PlainPrefix = true };

		var labels = scheme.Labels(new ArrTagUser(GuestId, "Guest"), Some.Of(new ArrTagFeed(FeedId, "Latest movies")));

		await Assert.That(labels).IsEquivalentTo(["sammlarr", "sammlarr-u-guest", "sammlarr-latest-movies"], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Labels_ForATitleAddedByHand_CarriesNoFeedTag()
	{
		await Assert.That(Testee.Labels(new ArrTagUser(GuestId, "Guest"), new None())).IsEquivalentTo(["sammlarr-u-guest"]);
	}

	[Test]
	public async Task Labels_WhenTheUserAndFeedPartsAreOff_CarriesOnlyThePlainPrefix()
	{
		var scheme = Testee with { PerUser = false, PerFeed = false, PlainPrefix = true };

		var labels = scheme.Labels(new ArrTagUser(GuestId, "Guest"), Some.Of(new ArrTagFeed(FeedId, "Latest movies")));

		await Assert.That(labels).IsEquivalentTo(["sammlarr"]);
	}

	[Test]
	public async Task Labels_WhenUserTagsAreOffForATitleAddedByHand_StillCarryThePrefix()
	{
		var scheme = Testee with { PerUser = false };

		await Assert.That(scheme.Labels(new ArrTagUser(GuestId, "Guest"), new None())).IsEquivalentTo(["sammlarr"]);
	}

	[Test]
	public async Task FeedTag_ForAListNamedLikeAUsersTag_NeverPosesAsOne()
	{
		var tag = Testee.FeedTag(new ArrTagFeed(FeedId, "U-Turn"));

		await Assert.That(tag).IsEqualTo("sammlarr-f-u-turn");
		await Assert.That(Testee.IsUserTag(tag)).IsFalse();
	}

	[Test]
	public async Task UserTag_ForAPrefixTypedWithCapitalsAndSpaces_UsesItSlugged()
	{
		var scheme = Testee with { Prefix = " Media Sync " };

		await Assert.That(scheme.UserTag(new ArrTagUser(GuestId, "Guest"))).IsEqualTo("media-sync-u-guest");
		await Assert.That(scheme.Owns("media-sync-u-guest")).IsTrue();
	}

	[Test]
	public async Task Slug_ForATypedPrefix_IsWhatTheTagsWouldStartWith()
	{
		await Assert.That(ArrTagScheme.Slug(" Media Sync ")).IsSome().And.IsEqualTo("media-sync");
	}

	[Test]
	public async Task Slug_ForAPrefixWithNothingUsable_IsNone()
	{
		await Assert.That(ArrTagScheme.Slug("🎬")).IsNone();
	}

	[Test]
	public async Task FeedTag_ForAListNamedKept_NeverPosesAsTheAdoptionMarker()
	{
		await Assert.That(Testee.FeedTag(new ArrTagFeed(FeedId, "Kept"))).IsEqualTo("sammlarr-f-kept");
	}

	[Test]
	public async Task UserTags_ForAUser_ListTheTagFromTheirNameAndFromTheirId()
	{
		await Assert.That(Testee.UserTags(new ArrTagUser(GuestId, "Guest"))).IsEquivalentTo(["sammlarr-u-guest", "sammlarr-u-8f14e45f"]);
	}

	[Test]
	[Arguments("sammlarr-u-guest", true)]
	[Arguments("sammlarr-latest", false)]
	[Arguments("sammlarr", false)]
	public async Task IsUserTag_ForALabel_ClaimsOnlyTheUserMarker(string label, bool user)
	{
		await Assert.That(Testee.IsUserTag(label)).IsEqualTo(user);
	}

	[Test]
	[Arguments("sammlarr", true)]
	[Arguments("sammlarr-u-guest", true)]
	[Arguments("sammlarrx", false)]
	[Arguments("4k", false)]
	public async Task Owns_ForALabel_ClaimsOnlyThePrefixAndItsChildren(string label, bool owned)
	{
		await Assert.That(Testee.Owns(label)).IsEqualTo(owned);
	}
}
