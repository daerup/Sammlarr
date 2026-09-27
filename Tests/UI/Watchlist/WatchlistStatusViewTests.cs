using UI.Features.Watchlist.Components;
using UI.Shared.Filters;

namespace UI.Tests.Watchlist;

public class WatchlistStatusViewTests
{
	private static readonly string[] Active = ["dune", "arrival"];
	private static readonly string[] Removed = ["tenet"];

	[Test]
	public async Task Within_WithNoStatusChosen_ShowsBothTheActiveAndThePendingRemovals()
	{
		var shown = WatchlistStatusView.Within(ListFilter.None, Active, Removed);

		await Assert.That(shown).IsEquivalentTo(new[] { "dune", "arrival", "tenet" });
	}

	[Test]
	public async Task Within_WithOnlyTheRemovedStatusChosen_ShowsThePendingRemovalsAlone()
	{
		var filter = ListFilter.None with { Statuses = [WatchlistStatusView.Removed] };

		var shown = WatchlistStatusView.Within(filter, Active, Removed);

		await Assert.That(shown).IsEquivalentTo(new[] { "tenet" });
	}

	[Test]
	public async Task Within_WithOnlyTheActiveStatusChosen_LeavesThePendingRemovalsOut()
	{
		var filter = ListFilter.None with { Statuses = [WatchlistStatusView.Active] };

		var shown = WatchlistStatusView.Within(filter, Active, Removed);

		await Assert.That(shown).IsEquivalentTo(new[] { "dune", "arrival" });
	}

	[Test]
	public async Task Adjusted_WhenEverythingLeftIsPendingRemoval_SwitchesTheDefaultOverToThem()
	{
		var statuses = WatchlistStatusView.Adjusted([WatchlistStatusView.Active], activeCount: 0, removedCount: 3);

		await Assert.That(statuses).IsEquivalentTo(new[] { WatchlistStatusView.Removed });
	}

	[Test]
	public async Task Adjusted_OnceThePendingRemovalsAreGone_GoesBackToTheActiveDefault()
	{
		var statuses = WatchlistStatusView.Adjusted([WatchlistStatusView.Removed], activeCount: 2, removedCount: 0);

		await Assert.That(statuses).IsEquivalentTo(new[] { WatchlistStatusView.Active });
	}

	[Test]
	public async Task Adjusted_WithBothStatusesPicked_LeavesTheChoiceAlone()
	{
		string[] both = [WatchlistStatusView.Active, WatchlistStatusView.Removed];

		var statuses = WatchlistStatusView.Adjusted(both, activeCount: 0, removedCount: 3);

		await Assert.That(statuses).IsEquivalentTo(both);
	}

	[Test]
	public async Task Options_WithNothingPendingRemoval_OffersNoStatusFilterAtAll()
	{
		await Assert.That(WatchlistStatusView.Options(0)).IsEmpty();
	}

	[Test]
	public async Task Options_WithPendingRemovals_CountsThemInTheLabel()
	{
		var options = WatchlistStatusView.Options(3);

		await Assert.That(options.Select(option => option.Label)).IsEquivalentTo(new[] { "On your watchlist", "Pending removal (3)" });
	}
}
