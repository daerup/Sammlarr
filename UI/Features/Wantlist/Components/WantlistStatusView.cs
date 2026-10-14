using Domain.Wantlist;
using Domain.Wantlist.Outbox;
using UI.Shared.Filters;

namespace UI.Features.Wantlist.Components;

internal static class WantlistStatusView
{
	internal const string Active = "active";
	internal const string Removed = "removed";

	internal static IReadOnlyList<StatusOption> Options(int removedCount) =>
		removedCount == 0
			? []
			: [new StatusOption(Active, "On your wantlist"), new StatusOption(Removed, FormattableString.CurrentCulture($"Pending removal ({removedCount})"))];

	internal static IReadOnlyList<string> Adjusted(IReadOnlyList<string> statuses, int activeCount, int removedCount) =>
		statuses switch
		{
			[Active] when activeCount == 0 && removedCount > 0 => [Removed],
			[Removed] when removedCount == 0 => [Active],
			_ => statuses,
		};

	internal static IReadOnlyList<T> Within<T>(ListFilter filter, IReadOnlyList<T> active, IReadOnlyList<T> removed)
	{
		var unfiltered = filter.Statuses.Count == 0;
		var shownActive = unfiltered || filter.HasStatus(Active) ? active : [];
		var shownRemoved = unfiltered || filter.HasStatus(Removed) ? removed : [];
		return shownRemoved.Count == 0 ? shownActive : [.. shownActive.Concat(shownRemoved)];
	}

	internal static IReadOnlyList<WantlistItem> WithPendingRemovals(WantlistSnapshot wantlist, OutboxSnapshot outbox) =>
		[
			.. wantlist
				.Items.Concat(outbox.Removes.Values.Select(entry => new WantlistItem(entry.MediaId, entry.AddedAt, new ManualOrigin(ManualSource.Unknown))))
				.OrderByDescending(item => item.AddedAt),
		];
}
