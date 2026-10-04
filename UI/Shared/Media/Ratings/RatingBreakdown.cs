using Domain.Media;

namespace UI.Shared.Media.Ratings;

internal enum RatingRowState
{
	Counts,
	NotNeeded,
	SwitchedOff,
	NoRating,
	NeedsProvider,
}

internal sealed record RatingRow(RatingSource Source, Optional<IMediaRating> Rating, RatingRowState State, Optional<int> Rank, bool SaysWhy);

internal sealed record RatingBreakdown(Optional<SammlarrRating> Sammlarr, IReadOnlyList<RatingRow> Rows, bool Ranked, bool UsedInstead, bool Partial)
{
	public static RatingBreakdown Of(RatingSummary ratings, RatingStrategy strategy, ValueSet<RatingSource> available, bool storedOnly = false)
	{
		var sammlarr = strategy.Resolve(ratings, available);
		var (states, ranked, usedInstead) = strategy switch
		{
			AveragedRating averaged => (Averaged(ratings, averaged, available), false, averaged.FallsBack(available)),
			OrderedRating ordered => (Ordered(ratings, ordered, available, sammlarr), true, false),
		};
		var kept = states.Where(entry => !storedOnly || entry.State != RatingRowState.NoRating).ToList();
		return new RatingBreakdown(sammlarr, RowsOf(kept, ratings, ranked), ranked, usedInstead, Partial: kept.Count < states.Count);
	}

	private static List<(RatingSource Source, RatingRowState State)> Averaged(RatingSummary ratings, AveragedRating averaged, ValueSet<RatingSource> available)
	{
		var counted = averaged.Counted(available);
		var needed = averaged.Missing(available).Select(source => (source, RatingRowState.NeedsProvider));
		var listed = Enum.GetValues<RatingSource>().Where(available.Contains).Select(source => (source, Averaging(source, counted, ratings)));
		return [.. needed, .. listed];
	}

	private static List<(RatingSource Source, RatingRowState State)> Ordered(
		RatingSummary ratings,
		OrderedRating ordered,
		ValueSet<RatingSource> available,
		Optional<SammlarrRating> sammlarr
	)
	{
		var first = sammlarr.Map(rating => rating.From[0].Source);
		return [.. ordered.Order.Where(available.Contains).Select(source => (source, Ordering(source, first, ratings)))];
	}

	private static RatingRowState Averaging(RatingSource source, ValueSet<RatingSource> counted, RatingSummary ratings)
	{
		if (!counted.Contains(source))
		{
			return RatingRowState.SwitchedOff;
		}

		return ratings.Of(source).Match(_ => RatingRowState.Counts, () => RatingRowState.NoRating);
	}

	private static RatingRowState Ordering(RatingSource source, Optional<RatingSource> first, RatingSummary ratings)
	{
		return ratings.Of(source).Match(_ => Picked(source, first), () => RatingRowState.NoRating);
	}

	private static RatingRowState Picked(RatingSource source, Optional<RatingSource> first)
	{
		return first.When(picked => picked == source).Match(_ => RatingRowState.Counts, () => RatingRowState.NotNeeded);
	}

	private static List<RatingRow> RowsOf(IReadOnlyList<(RatingSource Source, RatingRowState State)> states, RatingSummary ratings, bool ranked)
	{
		var everyRowUnrated = states.All(entry => entry.State == RatingRowState.NoRating);
		var said = new HashSet<RatingRowState>();
		var rows = new List<RatingRow>();
		foreach (var (source, state) in states)
		{
			var saysWhy = state switch
			{
				RatingRowState.Counts => false,
				RatingRowState.NeedsProvider => true,
				RatingRowState.NotNeeded or RatingRowState.SwitchedOff or RatingRowState.NoRating => !everyRowUnrated && said.Add(state),
			};
			var rating = ratings.Of(source).When(_ => state is not RatingRowState.NeedsProvider);
			rows.Add(new RatingRow(source, rating, state, Optional.When(ranked, () => rows.Count + 1), saysWhy));
		}

		return rows;
	}
}
