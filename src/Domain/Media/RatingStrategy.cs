namespace Domain.Media;

public readonly union RatingStrategy(OrderedRating, AveragedRating)
{
	public Optional<SammlarrRating> Resolve(MediaRatings ratings, ValueSet<RatingSource> available)
	{
		return this switch
		{
			OrderedRating ordered => ordered.Resolve(ratings, available),
			AveragedRating averaged => averaged.Resolve(ratings, available),
		};
	}
}
