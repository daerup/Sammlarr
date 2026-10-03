using CsCheck;
using Domain.Media;

namespace Domain.Tests.Media;

public class RatingStrategyProperties
{
	private static readonly RatingSource[] Sources = Enum.GetValues<RatingSource>();

	private static readonly Gen<RatingSummary> Summaries = Gen.Select(Gen.Bool.Array[Sources.Length], Gen.Int[0, 100].Array[Sources.Length])
		.Select(
			(held, values) => RatingSummary.From(Sources.Where((_, index) => held[index]).Select(source => source.Rate(values[(int)source] / 10f, new None())))
		);

	private static readonly Gen<ValueSet<RatingSource>> Availables = Gen.Bool.Array[Sources.Length].Select(Picked);

	private static readonly Gen<RatingSource[]> Orders = Gen.Shuffle(Sources);

	[Test]
	public void Resolve_WhenAveragingEverySource_GivesTheMeanOfTheAvailableRatings()
	{
		Gen.Select(Summaries, Availables).Sample(IsTheMeanOfTheAvailableRatings);
	}

	[Test]
	public void Resolve_WhenFirstAvailable_GivesTheFirstAvailableRatingInTheOrder()
	{
		Gen.Select(Summaries, Availables, Orders).Sample(IsTheFirstAvailableRating);
	}

	[Test]
	public void Resolve_WhenNoneOfTheAveragedSourcesIsAvailable_EqualsTheAverageOverEveryAvailableSource()
	{
		Gen.Select(Summaries, Availables).Sample(FallsBackToEveryAvailableSource);
	}

	private static bool IsTheMeanOfTheAvailableRatings(RatingSummary summary, ValueSet<RatingSource> available)
	{
		var values = summary.Sources.Where(rating => available.Contains(rating.Source)).Select(rating => rating.SammlarrValue).ToList();
		return AveragedRating
			.Everything.Resolve(summary, available)
			.Match(rating => values.Count > 0 && Close(rating.Value, values.Average()), () => values.Count == 0);
	}

	private static bool IsTheFirstAvailableRating(RatingSummary summary, ValueSet<RatingSource> available, RatingSource[] order)
	{
		var rated = order.Where(source => available.Contains(source) && summary.Of(source).IsSome()).ToList();
		return new OrderedRating(order)
			.Resolve(summary, available)
			.Match(rating => rated.Count > 0 && rating.From.Single().Source == rated[0], () => rated.Count == 0);
	}

	private static bool FallsBackToEveryAvailableSource(RatingSummary summary, ValueSet<RatingSource> available)
	{
		ValueSet<RatingSource> unavailable = [.. Sources.Except(available)];
		var averaged = new AveragedRating(unavailable).Resolve(summary, available).Map(rating => rating.Value);
		var everything = AveragedRating.Everything.Resolve(summary, available).Map(rating => rating.Value);
		return unavailable.Count == 0 || averaged.Match(value => everything.Match(other => Close(value, other), () => false), () => everything.IsNone());
	}

	private static ValueSet<RatingSource> Picked(bool[] flags) => [.. Sources.Where((_, index) => flags[index])];

	private static bool Close(float value, double expected) => Math.Abs(value - expected) < 0.0001;
}
