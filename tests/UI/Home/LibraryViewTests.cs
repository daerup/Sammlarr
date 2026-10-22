using CsCheck;
using Domain.Media;
using Domain.Providers;
using Domain.Wantlist;
using TestInfrastructure;
using TUnit.Assertions.Enums;
using UI.Features.Home.Components;
using UI.Shared.Services.Branding;

namespace UI.Tests.Home;

public class LibraryViewTests
{
	private static readonly MediaProviderBrandingService Branding = new();

	private static readonly IReadOnlyDictionary<MediaId, MediaType> NoTypes = new Dictionary<MediaId, MediaType>();

	[Test]
	public async Task Sources_ForThePlexLibrary_IsTheFeedThenPlexThenHand()
	{
		var wantlist = Wantlist([.. Many(2, Feed("d")), .. Many(53, new SinkOrigin(SinkKind.Plex)), .. Many(1, new ManualOrigin(ManualSource.Search))]);

		var shares = LibraryView.Sources(wantlist, NoTypes, Branding);

		await Assert
			.That(shares.Select(share => (share.Name, share.Count)))
			.IsEquivalentTo([("d", 2), ("From Plex", 53), ("Added by hand", 1)], CollectionOrdering.Matching);
		await Assert.That(shares[1].Color).IsSome().And.IsEqualTo(Branding.GetBrandingInfo(RemoteService.Plex).BrandColor);
		await Assert.That(shares[2].Color).IsSome().And.IsEqualTo(LibraryView.HandColor);
	}

	[Test]
	public async Task Sources_AfterASwitchToArr_KeepsPlexBesideRadarrMoviesAndSonarrShows()
	{
		var movies = Many(25, new SinkOrigin(SinkKind.Arr));
		var shows = Many(28, new SinkOrigin(SinkKind.Arr));
		var types = movies.Select(item => (item.MediaId, MediaType.Movie)).Concat(shows.Select(item => (item.MediaId, MediaType.Show))).ToDictionary();

		var shares = LibraryView.Sources(Wantlist([.. Many(4, new SinkOrigin(SinkKind.Plex)), .. movies, .. shows]), types, Branding);

		await Assert
			.That(shares.Select(share => (share.Name, share.Count)))
			.IsEquivalentTo([("From Plex", 4), ("From Radarr", 25), ("From Sonarr", 28)], CollectionOrdering.Matching);
	}

	[Test]
	public async Task Sources_ForAnArrTitleOfUnknownType_CountsItUnderRadarrAndSonarr()
	{
		var shares = LibraryView.Sources(Wantlist([Item(new SinkOrigin(SinkKind.Arr))]), NoTypes, Branding);

		await Assert.That(shares.Single().Name).IsEqualTo("From Radarr + Sonarr");
		await Assert.That(shares.Single().Color).IsSome();
	}

	[Test]
	public async Task Sources_ForADeletedFeed_NamesItDeletedAfterTheCurrentFeeds()
	{
		var shares = LibraryView.Sources(Wantlist([Item(new OrphanedOrigin("Old comedy")), Item(Feed("d"))]), NoTypes, Branding);

		await Assert.That(shares.Select(share => share.Name)).IsEquivalentTo(["d", "Old comedy (deleted)"], CollectionOrdering.Matching);
		await Assert.That(shares[1].Color).IsNone();
	}

	[Test]
	public async Task Sources_ForTwoFeeds_PutsTheBiggerFirst()
	{
		var shares = LibraryView.Sources(Wantlist([.. Many(1, Feed("a")), .. Many(3, Feed("b"))]), NoTypes, Branding);

		await Assert.That(shares.Select(share => share.Name)).IsEquivalentTo(["b", "a"], CollectionOrdering.Matching);
	}

	[Test]
	public void Sources_ForAnyWantlist_AddsUpToItsCountWithEveryUncolouredShareFirst()
	{
		Gen.Int[0, 4]
			.Select(Gen.Enum<MediaType>())
			.Array[0, 30]
			.Sample(picks =>
			{
				var items = picks.Select(pick => (Item: Item(Origin(pick.Item1)), Type: pick.Item2)).ToList();
				var shares = LibraryView.Sources(
					Wantlist([.. items.Select(pick => pick.Item)]),
					items.ToDictionary(pick => pick.Item.MediaId, pick => pick.Type),
					Branding
				);
				var coloured = shares.Select(share => share.Color.Match(_ => true, () => false)).ToList();
				return shares.Sum(share => share.Count) == items.Count && coloured.SkipWhile(colour => !colour).All(colour => colour);
			});
	}

	[Test]
	public async Task Sources_ForAnEmptyWantlist_IsEmpty()
	{
		await Assert.That(LibraryView.Sources(Wantlist([]), NoTypes, Branding)).IsEmpty();
	}

	[Test]
	public async Task ArrTitles_PicksOnlyWhatArrAdded()
	{
		var arr = Item(new SinkOrigin(SinkKind.Arr));

		var titles = LibraryView.ArrTitles(Wantlist([arr, Item(new SinkOrigin(SinkKind.Plex)), Item(Feed("d"))]));

		await Assert.That(titles).IsEquivalentTo([arr.MediaId]);
	}

	private static WantlistItemProvenance Origin(int kind)
	{
		return kind switch
		{
			0 => Feed("d"),
			1 => new OrphanedOrigin("Old comedy"),
			2 => new SinkOrigin(SinkKind.Plex),
			3 => new SinkOrigin(SinkKind.Arr),
			_ => new ManualOrigin(ManualSource.Browse),
		};
	}

	private static FeedOrigin Feed(string name) => TestFeeds.Origin(Guid.NewGuid(), Guid.NewGuid(), name);

	private static WantlistItem Item(WantlistItemProvenance provenance) => new(MediaId.New(), DateTimeOffset.UnixEpoch, provenance);

	private static WantlistItem[] Many(int count, WantlistItemProvenance provenance)
	{
		return [.. Enumerable.Range(0, count).Select(_ => Item(provenance))];
	}

	private static WantlistSnapshot Wantlist(IReadOnlyList<WantlistItem> items) => new(items);
}
