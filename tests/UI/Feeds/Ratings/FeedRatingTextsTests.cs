using Domain.Media;
using UI.Features.Feeds.Pages.Editor.Ratings;

namespace UI.Tests.Feeds.Ratings;

public class FeedRatingTextsTests
{
	private static readonly RatingStrategy Average = new(AveragedRating.Everything);

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
