using Domain.Media;
using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using TUnit.Assertions.Enums;
using UI.Features.Wantlist.Components;
using UI.Shared.Filters;

namespace UI.Tests.Wantlist;

public class WantlistStatusViewTests
{
	private static readonly string[] Active = ["dune", "arrival"];
	private static readonly string[] Removed = ["tenet"];

	[Test]
	public async Task Within_WithNoStatusChosen_ShowsBothTheActiveAndThePendingRemovals()
	{
		var shown = WantlistStatusView.Within(ListFilter.None, Active, Removed);

		await Assert.That(shown).IsEquivalentTo(new[] { "dune", "arrival", "tenet" });
	}

	[Test]
	public async Task Within_WithOnlyTheRemovedStatusChosen_ShowsThePendingRemovalsAlone()
	{
		var filter = ListFilter.None with { Statuses = [WantlistStatusView.Removed] };

		var shown = WantlistStatusView.Within(filter, Active, Removed);

		await Assert.That(shown).IsEquivalentTo(new[] { "tenet" });
	}

	[Test]
	public async Task Within_WithOnlyTheActiveStatusChosen_LeavesThePendingRemovalsOut()
	{
		var filter = ListFilter.None with { Statuses = [WantlistStatusView.Active] };

		var shown = WantlistStatusView.Within(filter, Active, Removed);

		await Assert.That(shown).IsEquivalentTo(new[] { "dune", "arrival" });
	}

	[Test]
	public async Task Adjusted_WhenEverythingLeftIsPendingRemoval_SwitchesTheDefaultOverToThem()
	{
		var statuses = WantlistStatusView.Adjusted([WantlistStatusView.Active], activeCount: 0, removedCount: 3);

		await Assert.That(statuses).IsEquivalentTo(new[] { WantlistStatusView.Removed });
	}

	[Test]
	public async Task Adjusted_OnceThePendingRemovalsAreGone_GoesBackToTheActiveDefault()
	{
		var statuses = WantlistStatusView.Adjusted([WantlistStatusView.Removed], activeCount: 2, removedCount: 0);

		await Assert.That(statuses).IsEquivalentTo(new[] { WantlistStatusView.Active });
	}

	[Test]
	public async Task Adjusted_WithBothStatusesPicked_LeavesTheChoiceAlone()
	{
		string[] both = [WantlistStatusView.Active, WantlistStatusView.Removed];

		var statuses = WantlistStatusView.Adjusted(both, activeCount: 0, removedCount: 3);

		await Assert.That(statuses).IsEquivalentTo(both);
	}

	[Test]
	public async Task Options_WithNothingPendingRemoval_OffersNoStatusFilterAtAll()
	{
		await Assert.That(WantlistStatusView.Options(0)).IsEmpty();
	}

	[Test]
	public async Task Options_WithPendingRemovals_CountsThemInTheLabel()
	{
		var options = WantlistStatusView.Options(3);

		await Assert.That(options.Select(option => option.Label)).IsEquivalentTo(new[] { "On your wantlist", "Pending removal (3)" });
	}

	[Test]
	public async Task WithPendingRemovals_ListsThePendingRemovalAtItsOriginalAddedAt_NewestFirst()
	{
		var wantlist = new WantlistSnapshot([new WantlistItem(Dune, On(2), new ManualOrigin(ManualSource.Search))]);

		var listed = WantlistStatusView.WithPendingRemovals(wantlist, [Queued(Tenet, WantlistOperationAction.Remove, On(1))]);

		await Assert.That(listed.Select(item => item.MediaId)).IsEquivalentTo([Dune, Tenet], CollectionOrdering.Matching);
		await Assert.That(listed[1].AddedAt).IsEqualTo(On(1));
	}

	[Test]
	public async Task WithPendingRemovals_LeavesQueuedAddsOut()
	{
		var listed = WantlistStatusView.WithPendingRemovals(new WantlistSnapshot([]), [Queued(Dune, WantlistOperationAction.Add, On(1))]);

		await Assert.That(listed).IsEmpty();
	}

	private static readonly MediaId Dune = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Tenet = MediaId.From(new Guid(2, 0, 0, new byte[8]));

	private static DateTimeOffset On(int day) => new(2026, 1, day, 0, 0, 0, TimeSpan.Zero);

	private static WantlistOutboxEntry Queued(MediaId id, WantlistOperationAction action, DateTimeOffset addedAt) =>
		new(id, action, WantlistOutboxState.Pending, 0) { AddedAt = addedAt };
}
