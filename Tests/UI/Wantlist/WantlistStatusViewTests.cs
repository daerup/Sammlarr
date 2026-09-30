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
}
