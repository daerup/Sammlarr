using System.Globalization;

namespace Domain.Media;

public readonly record struct SammlarrRating(float Value, IReadOnlyList<IMediaRating> From)
{
	public double Rounded
	{
		get
		{
			var reads = decimal.Parse(Value.ToString(CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
			return (double)Math.Round(reads, 1, MidpointRounding.AwayFromZero);
		}
	}

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

	public OrderedRating Moved(RatingSource source, int index)
	{
		var rest = Order.Where(other => other != source).ToList();
		rest.Insert(Math.Clamp(index, 0, rest.Count), source);
		return new OrderedRating(rest);
	}

	public bool Equals(OrderedRating other) => Order.SequenceEqual(other.Order);

	public override int GetHashCode() => Order.Aggregate(0, (hash, source) => HashCode.Combine(hash, source));
}

public readonly record struct AveragedRating(ValueSet<RatingSource> Sources)
{
	public static readonly AveragedRating Everything = new([]);

	public ValueSet<RatingSource> SwitchedOn => Sources.Count == 0 ? [.. Enum.GetValues<RatingSource>()] : Sources;

	public AveragedRating With(RatingSource source) => Of(SwitchedOn + [source]);

	public AveragedRating Without(RatingSource source)
	{
		ValueSet<RatingSource> rest = [.. SwitchedOn.Where(other => other != source)];
		return rest.Count == 0 ? this : Of(rest);
	}

	private static AveragedRating Of(ValueSet<RatingSource> sources)
	{
		return sources.Count == Enum.GetValues<RatingSource>().Length ? Everything : new AveragedRating(sources);
	}

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
