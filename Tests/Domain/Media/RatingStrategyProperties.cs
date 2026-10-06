using System.Globalization;
using CsCheck;
using Domain.Media;

namespace Domain.Tests.Media;

public class RatingStrategyProperties
{
	private static readonly RatingSource[] Sources = Enum.GetValues<RatingSource>();

	private static readonly Gen<MediaRatings> Ratings = Gen.Select(Gen.Bool.Array[Sources.Length], Gen.Int[0, 100].Array[Sources.Length])
		.Select(
			(held, values) => MediaRatings.From(Sources.Where((_, index) => held[index]).Select(source => source.Card.Rate(values[(int)source] / 10f, new None())))
		);

	private static readonly Gen<ValueSet<RatingSource>> Availables = Gen.Bool.Array[Sources.Length].Select(Picked);

	private static readonly Gen<RatingSource[]> Orders = Gen.Shuffle(Sources);

	[Test]
	public void Resolve_WhenAveragingEverySource_GivesTheMeanOfTheAvailableRatings()
	{
		Gen.Select(Ratings, Availables).Sample(IsTheMeanOfTheAvailableRatings);
	}

	[Test]
	public void Resolve_WhenFirstAvailable_GivesTheFirstAvailableRatingInTheOrder()
	{
		Gen.Select(Ratings, Availables, Orders).Sample(IsTheFirstAvailableRating);
	}

	[Test]
	public void Resolve_WhenNoneOfTheAveragedSourcesIsAvailable_EqualsTheAverageOverEveryAvailableSource()
	{
		Gen.Select(Ratings, Availables).Sample(FallsBackToEveryAvailableSource);
	}

	[Test]
	public void Rounded_ForAnyRating_ReadsAsItsValueReads()
	{
		Gen.OneOf(Gen.Float[0f, 10f], Gen.Int[0, 99].Select(tenth => (float)(tenth / 10.0 + 0.05)), Gen.Int[0, 40].Select(quarter => quarter / 4f))
			.Sample(value => new SammlarrRating(value, []).Rounded.ToString("0.0", CultureInfo.InvariantCulture) == Text(value));
	}

	private static bool IsTheMeanOfTheAvailableRatings(MediaRatings ratings, ValueSet<RatingSource> available)
	{
		var values = ratings.All.Where(rating => available.Contains(rating.Source)).Select(rating => rating.SammlarrValue).ToList();
		return AveragedRating
			.Everything.Resolve(ratings, available)
			.Match(rating => values.Count > 0 && Close(rating.Value, values.Average()), () => values.Count == 0);
	}

	private static bool IsTheFirstAvailableRating(MediaRatings ratings, ValueSet<RatingSource> available, RatingSource[] order)
	{
		var rated = order.Where(source => available.Contains(source) && ratings.Of(source).IsSome()).ToList();
		return new OrderedRating(order)
			.Resolve(ratings, available)
			.Match(rating => rated.Count > 0 && rating.Counted.Single().Source == rated[0], () => rated.Count == 0);
	}

	private static bool FallsBackToEveryAvailableSource(MediaRatings ratings, ValueSet<RatingSource> available)
	{
		ValueSet<RatingSource> unavailable = [.. Sources.Except(available)];
		var averaged = new AveragedRating(unavailable).Resolve(ratings, available).Map(rating => rating.Value);
		var everything = AveragedRating.Everything.Resolve(ratings, available).Map(rating => rating.Value);
		return unavailable.Count == 0 || averaged.Match(value => everything.Match(other => Close(value, other), () => false), () => everything.IsNone());
	}

	private static ValueSet<RatingSource> Picked(bool[] flags) => [.. Sources.Where((_, index) => flags[index])];

	private static bool Close(float value, double expected) => Math.Abs(value - expected) < 0.0001;

	private static string Text(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
