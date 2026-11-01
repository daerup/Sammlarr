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
		return (args.AltKey, args.Key) switch
		{
			(true, "ArrowUp") => By(ordered, rows, source, -1),
			(true, "ArrowDown") => By(ordered, rows, source, 1),
			_ => new None(),
		};
	}

	public static Optional<(OrderedRating Order, int Index)> By(OrderedRating ordered, IReadOnlyList<RatingSource> rows, RatingSource source, int step)
	{
		var target = rows.ToList().IndexOf(source) + step;
		return Optional.When(target >= 0 && target < rows.Count, () => (Onto(ordered, source, rows[target]), target));
	}

	public static OrderedRating Onto(OrderedRating ordered, RatingSource source, RatingSource target)
	{
		return ordered.Moved(source, ordered.Order.ToList().IndexOf(target));
	}
}
