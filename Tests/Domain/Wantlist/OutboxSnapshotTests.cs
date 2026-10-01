using Domain.Media;
using Domain.Wantlist.Outbox;

namespace Domain.Tests.Wantlist;

public class OutboxSnapshotTests
{
	private static readonly MediaId Dune = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Tenet = MediaId.From(new Guid(2, 0, 0, new byte[8]));
	private static readonly MediaId Heat = MediaId.From(new Guid(3, 0, 0, new byte[8]));

	private static readonly OutboxSnapshot Testee = new([
		Entry(Dune, WantlistOperationAction.Add, WantlistOutboxState.Pending),
		Entry(Tenet, WantlistOperationAction.Remove, WantlistOutboxState.Pending),
		Entry(Heat, WantlistOperationAction.Add, WantlistOutboxState.Failed),
	]);

	[Test]
	public async Task Pending_LeavesFailedEntriesOut() => await Assert.That(Testee.Pending.Select(entry => entry.MediaId)).IsEquivalentTo([Dune, Tenet]);

	[Test]
	public async Task Adds_HoldEveryQueuedAdd_WhateverItsState() => await Assert.That(Testee.Adds.Keys).IsEquivalentTo([Dune, Heat]);

	[Test]
	public async Task Removes_HoldEveryQueuedRemoval() => await Assert.That(Testee.Removes.Keys).IsEquivalentTo([Tenet]);

	private static WantlistOutboxEntry Entry(MediaId id, WantlistOperationAction action, WantlistOutboxState state) => new(id, action, state, 0);
}
