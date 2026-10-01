using Domain.Media;
using Domain.Wantlist;
using TestInfrastructure;

namespace Domain.Tests.Wantlist;

public class WantlistSnapshotTests
{
	private static readonly WantlistItemProvenance Manual = new(new ManualOrigin(ManualSource.Search));
	private static readonly MediaId Dune = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Arrival = MediaId.From(new Guid(2, 0, 0, new byte[8]));
	private static readonly MediaId Tenet = MediaId.From(new Guid(3, 0, 0, new byte[8]));

	private static readonly WantlistSnapshot Testee = new([Item(Dune), Item(Arrival)]);

	[Test]
	public async Task Contains_ForAWantlistedTitle_IsTrue() => await Assert.That(Testee.Contains(Dune)).IsTrue();

	[Test]
	public async Task Contains_ForATitleNotOnTheWantlist_IsFalse() => await Assert.That(Testee.Contains(Tenet)).IsFalse();

	[Test]
	public async Task Find_ForAWantlistedTitle_ReturnsItsItem() => await Assert.That(Testee.Find(Arrival)).IsSome().And.IsEqualTo(Item(Arrival));

	[Test]
	public async Task Find_ForATitleNotOnTheWantlist_ReturnsNone() => await Assert.That(Testee.Find(Tenet)).IsNone();

	[Test]
	public async Task Ids_HoldEveryWantlistedTitle() => await Assert.That(Testee.Ids).IsEquivalentTo([Dune, Arrival]);

	[Test]
	public async Task Items_HoldEveryWantlistedItem() => await Assert.That(Testee.Items).IsEquivalentTo([Item(Dune), Item(Arrival)]);

	private static WantlistItem Item(MediaId id) => new(id, DateTimeOffset.UnixEpoch, Manual);
}
