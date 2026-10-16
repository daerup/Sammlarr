using Domain.Media;

namespace UI.Shared.Media.Ratings;

internal enum RatingRowState
{
	Counts,
	NotNeeded,
	SwitchedOff,
	NoRating,
	Skipped,
}

internal sealed record RatingRow(RatingSource Source, Optional<IMediaRating> Rating, RatingRowState State, Optional<int> Rank, bool SaysWhy);

internal sealed record RatingBreakdown(Optional<SammlarrRating> Sammlarr, IReadOnlyList<RatingRow> Rows, bool Ranked, bool UsedInstead, bool Partial)
{
	public static RatingBreakdown Of(MediaRatings ratings, RatingStrategy strategy, ValueSet<RatingSource> available, bool storedOnly = false)
	{
		var sammlarr = strategy.Resolve(ratings, available);
		var (states, ranked, usedInstead) = strategy switch
		{
			AveragedRating averaged => (Averaged(ratings, averaged, available), false, averaged.FallsBack(available)),
			OrderedRating ordered => (Ordered(ratings, ordered, available, sammlarr), true, false),
		};
		var stored = states.Where(entry => !storedOnly || entry.State is not (RatingRowState.NoRating or RatingRowState.Skipped)).ToList();
		var shown = stored.Where(entry => Shown(entry.State, ratings.Of(entry.Source))).ToList();
		return new RatingBreakdown(sammlarr, RowsOf(shown, ratings, ranked), ranked, usedInstead, Partial: stored.Count < states.Count);
	}

	private static bool Shown(RatingRowState state, Optional<IMediaRating> rating) => state is RatingRowState.Skipped || rating.IsSome();

	private static List<(RatingSource Source, RatingRowState State)> Averaged(MediaRatings ratings, AveragedRating averaged, ValueSet<RatingSource> available)
	{
		var counted = averaged.Counted(available);
		return [.. Enum.GetValues<RatingSource>().Where(available.Contains).Select(source => (source, Averaging(source, counted, ratings)))];
	}

	private static List<(RatingSource Source, RatingRowState State)> Ordered(
		MediaRatings ratings,
		OrderedRating ordered,
		ValueSet<RatingSource> available,
		Optional<SammlarrRating> sammlarr
	)
	{
		var first = sammlarr.Map(rating => rating.Counted[0].Source);
		var order = ordered.Order.Where(available.Contains).ToList();
		var pickedAt = first.Map(order.IndexOf).GetValueOr(-1);
		return [.. order.Select((source, index) => (source, Ordering(source, first, ratings, index < pickedAt)))];
	}

	private static RatingRowState Averaging(RatingSource source, ValueSet<RatingSource> counted, MediaRatings ratings)
	{
		if (!counted.Contains(source))
		{
			return RatingRowState.SwitchedOff;
		}

		return ratings.Of(source).Match(_ => RatingRowState.Counts, () => RatingRowState.NoRating);
	}

	private static RatingRowState Ordering(RatingSource source, Optional<RatingSource> first, MediaRatings ratings, bool abovePick)
	{
		return ratings.Of(source).Match(_ => Picked(source, first), () => abovePick ? RatingRowState.Skipped : RatingRowState.NoRating);
	}

	private static RatingRowState Picked(RatingSource source, Optional<RatingSource> first)
	{
		return first.When(picked => picked == source).Match(_ => RatingRowState.Counts, () => RatingRowState.NotNeeded);
	}

	private static List<RatingRow> RowsOf(IReadOnlyList<(RatingSource Source, RatingRowState State)> states, MediaRatings ratings, bool ranked)
	{
		var said = new HashSet<RatingRowState>();
		var rows = new List<RatingRow>();
		foreach (var (source, state) in states)
		{
			var saysWhy = state switch
			{
				RatingRowState.Counts => false,
				RatingRowState.Skipped => true,
				RatingRowState.NotNeeded or RatingRowState.SwitchedOff or RatingRowState.NoRating => said.Add(state),
			};
			rows.Add(new RatingRow(source, ratings.Of(source), state, Optional.When(ranked, () => rows.Count + 1), saysWhy));
		}

		return rows;
	}
}
