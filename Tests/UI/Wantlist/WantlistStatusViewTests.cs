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
	[Arguments(new string[] { }, new[] { "dune", "arrival", "tenet" })]
	[Arguments(new[] { WantlistStatusView.Removed }, new[] { "tenet" })]
	[Arguments(new[] { WantlistStatusView.Active }, new[] { "dune", "arrival" })]
	public async Task Within_ForTheChosenStatuses_ShowsOnlyTheirTitlesOrEverythingWhenNoneIsChosen(string[] statuses, string[] expected)
	{
		var shown = WantlistStatusView.Within(ListFilter.None with { Statuses = statuses }, Active, Removed);

		await Assert.That(shown).IsEquivalentTo(expected);
	}

	[Test]
	[Arguments(new[] { WantlistStatusView.Active }, 0, 3, new[] { WantlistStatusView.Removed })]
	[Arguments(new[] { WantlistStatusView.Removed }, 2, 0, new[] { WantlistStatusView.Active })]
	[Arguments(new[] { WantlistStatusView.Active, WantlistStatusView.Removed }, 0, 3, new[] { WantlistStatusView.Active, WantlistStatusView.Removed })]
	public async Task Adjusted_WhenTheOnlyChosenStatusHasNothingLeft_SwitchesToTheOtherButLeavesBothPickedAlone(
		string[] statuses,
		int activeCount,
		int removedCount,
		string[] expected
	)
	{
		var adjusted = WantlistStatusView.Adjusted(statuses, activeCount, removedCount);

		await Assert.That(adjusted).IsEquivalentTo(expected);
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

		var listed = WantlistStatusView.WithPendingRemovals(wantlist, new OutboxSnapshot([Queued(Tenet, WantlistOperationAction.Remove, On(1))]));

		await Assert.That(listed.Select(item => item.MediaId)).IsEquivalentTo([Dune, Tenet], CollectionOrdering.Matching);
		await Assert.That(listed[1].AddedAt).IsEqualTo(On(1));
	}

	private static readonly MediaId Dune = MediaId.From(new Guid(1, 0, 0, new byte[8]));
	private static readonly MediaId Tenet = MediaId.From(new Guid(2, 0, 0, new byte[8]));

	private static DateTimeOffset On(int day) => new(2026, 1, day, 0, 0, 0, TimeSpan.Zero);

	private static WantlistOutboxEntry Queued(MediaId id, WantlistOperationAction action, DateTimeOffset addedAt) =>
		new(id, action, WantlistOutboxState.Pending, 0) { AddedAt = addedAt };
}
