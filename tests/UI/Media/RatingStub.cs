using Contracts.Ratings;
using Domain.Media;
using UI.Shared.Services;

namespace UI.Tests.Media;

internal static class RatingStub
{
	internal static RatingContext Context()
	{
		var ratings = IRatingService.Mock();
		ratings.GetDefaultAsync(Any()).Returns(new RatingStrategy(AveragedRating.Everything));
		ratings.GetAvailableAsync(Any()).Returns([.. Enum.GetValues<RatingSource>()]);
		return new RatingContext(ratings.Object);
	}
}
