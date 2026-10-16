using Domain.Media;
using Microsoft.AspNetCore.Components.Web;

namespace UI.Shared.Media.Ratings;

internal static class RatingOrderMoves
{
	public static Optional<(OrderedRating Order, int Index)> ByKey(
		OrderedRating ordered,
		IReadOnlyList<RatingSource> rows,
		RatingSource source,
		KeyboardEventArgs args
	)
	{
		var index = rows.ToList().IndexOf(source);
		var target = (args.AltKey, args.Key) switch
		{
			(true, "ArrowUp") => index - 1,
			(true, "ArrowDown") => index + 1,
			_ => -1,
		};
		return Optional.When(target >= 0 && target < rows.Count, () => (Onto(ordered, source, rows[target]), target));
	}

	public static OrderedRating Onto(OrderedRating ordered, RatingSource source, RatingSource target)
	{
		return ordered.Moved(source, ordered.Order.ToList().IndexOf(target));
	}
}
