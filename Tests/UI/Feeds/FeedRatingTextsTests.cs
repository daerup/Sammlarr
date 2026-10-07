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
			.IsEqualTo(
				"This feed's Sammlarr rating: First available · IMDb, TMDB, RT critics, RT audience, Metacritic, Letterboxd, Trakt, MAL, AniList. Click to change."
			);
	}
}
