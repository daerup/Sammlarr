using Domain.Media;
using UI.Features.Feeds.Pages.Editor;

namespace UI.Tests.Feeds;

public class FeedRatingTextsTests
{
	private static readonly RatingStrategy Average = new(AveragedRating.Everything);

	[Test]
	public async Task StarTip_WhenTheFeedFollowsYourDefault_NamesTheDefaultsStrategy()
	{
		await Assert.That(FeedRatingTexts.StarTip(new None(), Average)).IsEqualTo("This feed's Sammlarr rating: My default (Average). Click to change.");
	}

	[Test]
	public async Task StarTip_WhenTheFeedHasItsOwnOrder_NamesTheOrder()
	{
		var ordered = new RatingStrategy(new OrderedRating([RatingSource.Imdb, RatingSource.Tmdb, RatingSource.RottenTomatoesCritics]));

		await Assert
			.That(FeedRatingTexts.StarTip(Some.Of(ordered), Average))
			.IsEqualTo("This feed's Sammlarr rating: First available · IMDb, TMDB, RT critics, RT audience. Click to change.");
	}

	[Test]
	public async Task VotesTip_UnderYourDefaultAverage_AddsUpTheVotesOfTheSwitchedOnProviders()
	{
		var averaged = new RatingStrategy(new AveragedRating([RatingSource.Tmdb, RatingSource.Imdb]));

		await Assert
			.That(FeedRatingTexts.VotesTip(new None(), averaged))
			.IsEqualTo(
				"Votes counts the people behind this feed's rating (your default, Average): the votes of every provider it averages (TMDB, IMDb), added up. Rotten Tomatoes publishes no votes, so it adds none."
			);
	}

	[Test]
	public async Task VotesTip_UnderTheFeedsFirstAvailable_TakesThePickedProvidersVotes()
	{
		var ordered = new RatingStrategy(new OrderedRating([RatingSource.RottenTomatoesCritics]));

		await Assert.That(FeedRatingTexts.VotesTip(Some.Of(ordered), Average)).Contains("(First available): the votes of the one provider that gave it");
		await Assert.That(FeedRatingTexts.VotesTip(Some.Of(ordered), Average)).EndsWith("so a title it rates fails a Votes rule.");
	}
}
