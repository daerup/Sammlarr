namespace Domain.Media;

public readonly record struct SammlarrRating(float Value, IReadOnlyList<IMediaRating> From)
{
	public Optional<int> Votes
	{
		get
		{
			var counts = From.Select(rating => rating.Votes).WhereSome().ToList();
			return Optional.When(counts.Count > 0, () => counts.Sum());
		}
	}
}

public readonly record struct OrderedRating
{
	public OrderedRating()
		: this([]) { }

	public OrderedRating(IEnumerable<RatingSource> order)
	{
		var given = order.Distinct().ToList();
		Order = [.. given, .. Enum.GetValues<RatingSource>().Except(given)];
	}

	public IReadOnlyList<RatingSource> Order { get; }

	public Optional<SammlarrRating> Resolve(RatingSummary ratings, ValueSet<RatingSource> available)
	{
		return Order
			.Select(ratings.Of)
			.WhereSome()
			.FirstOrNone(rating => available.Contains(rating.Source))
			.Map(rating => new SammlarrRating(rating.SammlarrValue, [rating]));
	}

	public bool Equals(OrderedRating other) => Order.SequenceEqual(other.Order);

	public override int GetHashCode() => Order.Aggregate(0, (hash, source) => HashCode.Combine(hash, source));
}

public readonly record struct AveragedRating(ValueSet<RatingSource> Sources)
{
	public static readonly AveragedRating Everything = new([]);

	public Optional<SammlarrRating> Resolve(RatingSummary ratings, ValueSet<RatingSource> available)
	{
		var counted = Counted(available);
		var from = ratings.Sources.Where(rating => counted.Contains(rating.Source)).ToList();
		return Optional.When(from.Count > 0, () => new SammlarrRating(from.Average(rating => rating.SammlarrValue), from));
	}

	public ValueSet<RatingSource> Counted(ValueSet<RatingSource> available)
	{
		var sources = Sources;
		ValueSet<RatingSource> kept = [.. available.Where(source => sources.Count == 0 || sources.Contains(source))];
		return kept.Count > 0 ? kept : available;
	}
}
