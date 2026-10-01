using Domain.Media;
using Domain.Wantlist.Sync;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Wantlist;

public class SinkSnapshotTests
{
	private static readonly MediaId Dune = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Tenet = MediaId.From(new Guid(2, 0, 0, new byte[8]));
	private static readonly MediaId Heat = MediaId.From(new Guid(3, 0, 0, new byte[8]));

	private static readonly SinkSnapshot Testee = new([Tenet, Dune], []);

	[Test]
	public async Task Contains_ForATitleOnTheSink_IsTrue() => await Assert.That(Testee.Contains(Dune)).IsTrue();

	[Test]
	public async Task Contains_ForATitleNotOnTheSink_IsFalse() => await Assert.That(Testee.Contains(Heat)).IsFalse();

	[Test]
	public async Task Ids_KeepTheSinksOrder() => await Assert.That(Testee.Ids).IsEquivalentTo([Tenet, Dune], CollectionOrdering.Matching);
}
