namespace Domain.Media;

public union RatingStrategy(OrderedRating, AveragedRating)
{
	public Optional<SammlarrRating> Resolve(RatingSummary ratings, ValueSet<RatingSource> available)
	{
		return this switch
		{
			OrderedRating ordered => ordered.Resolve(ratings, available),
			AveragedRating averaged => averaged.Resolve(ratings, available),
		};
	}
}
