using Domain.Media;
using TestInfrastructure;
using TUnit.Assertions.Enums;

namespace Domain.Tests.Media;

public class RatingStrategyTests
{
	private static readonly ValueSet<RatingSource> Everything =
	[
		RatingSource.Tmdb,
		RatingSource.Imdb,
		RatingSource.RottenTomatoesCritics,
		RatingSource.RottenTomatoesAudience,
	];

	private static readonly RatingSummary ProjectHailMary = RatingSummary.From([
		new TmdbRating(8.6f, Some.Of(7705)),
		new ImdbRating(8.2f, Some.Of(542195)),
		new RottenTomatoesCriticsRating(95),
		new RottenTomatoesAudienceRating(95),
	]);

	[Test]
	public async Task Resolve_WhenAveragingEverySource_TakesTheMeanOfEveryRating()
	{
		var rating = new RatingStrategy(AveragedRating.Everything).Resolve(ProjectHailMary, Everything);

		await Assert.That(rating).IsSome();
		await Assert.That(rating.Map(found => found.Value).GetValueOr(0)).IsEqualTo(8.95f).Within(0.001f);
	}

	[Test]
	public async Task Resolve_WhenAveragingSomeSources_LeavesTheSwitchedOffOnesOut()
	{
		var rating = new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])).Resolve(ProjectHailMary, Everything);

		await Assert.That(rating.Map(found => found.Value).GetValueOr(0)).IsEqualTo(8.85f).Within(0.001f);
	}

	[Test]
	public async Task Resolve_WhenNoneOfTheAveragedSourcesIsAvailable_AveragesEveryAvailableSource()
	{
		var rating = new RatingStrategy(new AveragedRating([RatingSource.Imdb, RatingSource.RottenTomatoesCritics])).Resolve(ProjectHailMary, [RatingSource.Tmdb]);

		await Assert.That(rating.Map(found => found.Value).GetValueOr(0)).IsEqualTo(8.6f);
	}

	[Test]
	public async Task Resolve_WhenSomeAveragedSourcesAreUnavailable_AveragesTheRest()
	{
		var rating = new RatingStrategy(new AveragedRating([RatingSource.Tmdb, RatingSource.Imdb])).Resolve(ProjectHailMary, [RatingSource.Tmdb]);

		await Assert.That(rating.Map(found => found.Value).GetValueOr(0)).IsEqualTo(8.6f);
	}

	[Test]
	public async Task Resolve_WhenFirstAvailable_TakesTheFirstRatingInTheOrder()
	{
		var rating = new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesCritics, RatingSource.Imdb])).Resolve(ProjectHailMary, Everything);

		await Assert.That(rating.Map(found => found.Value).GetValueOr(0)).IsEqualTo(9.5f);
	}

	[Test]
	public async Task Resolve_WhenTheFirstSourceIsUnavailable_SkipsToTheNextOne()
	{
		var rating = new RatingStrategy(new OrderedRating([RatingSource.Imdb, RatingSource.Tmdb])).Resolve(ProjectHailMary, [RatingSource.Tmdb]);

		await Assert.That(rating.Map(found => found.Value).GetValueOr(0)).IsEqualTo(8.6f);
	}

	[Test]
	public async Task Resolve_WhenTheTitleHasNoRating_GivesNone()
	{
		await Assert.That(new RatingStrategy(AveragedRating.Everything).Resolve(RatingSummary.None, Everything)).IsNone();
		await Assert.That(new RatingStrategy(new OrderedRating([])).Resolve(RatingSummary.None, Everything)).IsNone();
	}

	[Test]
	public async Task OrderedRating_WhenTheStoredOrderMissesASource_PutsItLast()
	{
		var ordered = new OrderedRating([RatingSource.Imdb, RatingSource.Imdb, RatingSource.Tmdb]);

		await Assert
			.That(ordered.Order)
			.IsEquivalentTo(
				[RatingSource.Imdb, RatingSource.Tmdb, RatingSource.RottenTomatoesCritics, RatingSource.RottenTomatoesAudience],
				CollectionOrdering.Matching
			);
	}

	[Test]
	public async Task OrderedRating_ComparesByItsOrder()
	{
		await Assert.That(new OrderedRating([RatingSource.Imdb])).IsEqualTo(new OrderedRating([RatingSource.Imdb]));
		await Assert.That(new OrderedRating([RatingSource.Imdb])).IsNotEqualTo(new OrderedRating([RatingSource.Tmdb]));
	}

	[Test]
	public async Task Votes_WhenAveraging_AddsTheCountedProvidersVotes()
	{
		var rating = new RatingStrategy(AveragedRating.Everything).Resolve(ProjectHailMary, Everything);

		await Assert.That(rating.Map(found => found.Votes)).IsSome().And.IsEqualTo(549900);
	}

	[Test]
	public async Task Votes_WhenFirstAvailable_TakesThePickedProvidersVotes()
	{
		var rating = new RatingStrategy(new OrderedRating([RatingSource.Imdb])).Resolve(ProjectHailMary, Everything);

		await Assert.That(rating.Map(found => found.Votes)).IsSome().And.IsEqualTo(542195);
	}

	[Test]
	public async Task Votes_WhenOnlyRottenTomatoesCounts_GivesNone()
	{
		var rating = new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesCritics])).Resolve(ProjectHailMary, Everything);

		await Assert.That(rating).IsSome();
		await Assert.That(rating.Map(found => found.Votes)).IsNone();
	}
}
