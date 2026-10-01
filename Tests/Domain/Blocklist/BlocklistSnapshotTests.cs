using Domain.Blocklist;
using Domain.Media;

namespace Domain.Tests.Blocklist;

public class BlocklistSnapshotTests
{
	private static readonly MediaId Saw = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Hostel = MediaId.From(new Guid(2, 0, 0, new byte[8]));
	private static readonly MediaId Paddington = MediaId.From(new Guid(3, 0, 0, new byte[8]));

	private static readonly BlocklistSnapshot Testee = new([Item(Saw), Item(Hostel)]);

	[Test]
	public async Task Contains_ForABlocklistedTitle_IsTrue() => await Assert.That(Testee.Contains(Saw)).IsTrue();

	[Test]
	public async Task Contains_ForATitleNotOnTheBlocklist_IsFalse() => await Assert.That(Testee.Contains(Paddington)).IsFalse();

	[Test]
	public async Task Ids_HoldEveryBlocklistedTitle() => await Assert.That(Testee.Ids).IsEquivalentTo([Saw, Hostel]);

	[Test]
	public async Task Items_HoldEveryBlocklistedItem() => await Assert.That(Testee.Items).IsEquivalentTo([Item(Saw), Item(Hostel)]);

	private static BlocklistItem Item(MediaId id) => new(id, DateTimeOffset.UnixEpoch);
}
